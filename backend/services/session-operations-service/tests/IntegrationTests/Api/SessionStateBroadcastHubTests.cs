using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class SessionStateBroadcastHubTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public SessionStateBroadcastHubTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.AccessClient.IsAllowed = true;
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            OperatorUserId,
            OperatorExternalIdentityId,
            "Operator",
            true);
        await _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task TransitioningSession_BroadcastsStateChangeToConnectedParticipant()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveSessionWithDisconnectedParticipantAsync(externalIdentityId);

        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");
        var received = new TaskCompletionSource<SessionStateChangedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        participant.On<SessionStateChangedNotificationDto>(
            SessionStateBroadcaster.StateChangedMethod,
            notification => received.TrySetResult(notification));

        await participant.StartAsync();

        // Joining the live-session group is what subscribes the participant to broadcasts.
        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var operatorClient = _factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-User-Id", OperatorExternalIdentityId);
        operatorClient.DefaultRequestHeaders.Add("X-User-Role", "Operator");
        operatorClient.DefaultRequestHeaders.Add("X-User-Email", "operator@example.com");

        var response = await operatorClient.PatchAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId:D}/state",
            new { targetState = "Paused", reason = "Break" });

        response.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task, "the transition must broadcast over SignalR");

        var notification = await received.Task;
        notification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        notification.PreviousState.Should().Be(nameof(SessionState.Active));
        notification.CurrentState.Should().Be(nameof(SessionState.Paused));
    }

    [Fact]
    public async Task AssignedOperatorJoin_ReceivesSessionStateChangedBroadcast()
    {
        var seeded = await SeedActiveSessionWithDisconnectedParticipantAsync(Guid.NewGuid());
        await using var operatorConnection = CreateHubConnection(OperatorExternalIdentityId, "Operator", "operator@example.com");
        var received = new TaskCompletionSource<SessionStateChangedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        operatorConnection.On<SessionStateChangedNotificationDto>(
            SessionStateBroadcaster.StateChangedMethod,
            notification => received.TrySetResult(notification));

        await operatorConnection.StartAsync();
        await operatorConnection.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);

        var operatorClient = _factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-User-Id", OperatorExternalIdentityId);
        operatorClient.DefaultRequestHeaders.Add("X-User-Role", "Operator");
        operatorClient.DefaultRequestHeaders.Add("X-User-Email", "operator@example.com");

        var response = await operatorClient.PatchAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId:D}/state",
            new { targetState = "Paused", reason = "Break" });

        response.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task);
        (await received.Task).CurrentState.Should().Be(nameof(SessionState.Paused));
    }

    [Fact]
    public async Task NonAssignedOperatorJoin_IsForbidden()
    {
        var seeded = await SeedActiveSessionWithDisconnectedParticipantAsync(Guid.NewGuid());
        _factory.AuthenticatedActorProfileAccessClient.CurrentActor = new AuthenticatedActorProfileLookupDto(
            77,
            "kc-operator-77",
            "Operator",
            true);
        await using var operatorConnection = CreateHubConnection("kc-operator-77", "Operator", "intruder@example.com");

        await operatorConnection.StartAsync();

        var act = () => operatorConnection.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);

        await act.Should().ThrowAsync<HubException>()
            .Where(exception => exception.Message.Contains("\"code\":\"FORBIDDEN\"", StringComparison.Ordinal));
    }

    private HubConnection CreateHubConnection(string userId, string role, string email)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, "/hubs/sessions"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Headers["X-User-Id"] = userId;
                    options.Headers["X-User-Role"] = role;
                    options.Headers["X-User-Email"] = email;
                })
            .Build();
    }

    private async Task<SeededSession> SeedActiveSessionWithDisconnectedParticipantAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var session = LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Broadcast Session",
            45,
            createdAt);
        var team = session.RegisterTeam("Red", "RED-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));
        var participant = session.AdmitParticipant(
            externalIdentityId,
            "Nova",
            team.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        session.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(5));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
