using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

[Collection(PostgreSqlCollection.Name)]
public sealed class OperatorSessionPanelHubTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";
    private const string OperatorPanelUpdatedMethod = "OperatorSessionPanelUpdated";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public OperatorSessionPanelHubTests(PostgreSqlFixture fixture)
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
    public async Task TransitioningSession_PushesUpdatedOperatorPanelOnlyToOperatorGroup()
    {
        var participantExternalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveTreasureHuntSessionWithDisconnectedParticipantAsync(participantExternalIdentityId);

        await using var operatorConnection = CreateHubConnection(
            OperatorExternalIdentityId,
            "Operator",
            "operator@example.com");
        await using var participantConnection = CreateHubConnection(
            participantExternalIdentityId.ToString(),
            "Participant",
            "participant@example.com");

        var operatorReceived = new TaskCompletionSource<OperatorSessionPanelDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var participantReceived = new TaskCompletionSource<OperatorSessionPanelDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        operatorConnection.On<OperatorSessionPanelDto>(
            OperatorPanelUpdatedMethod,
            panel => operatorReceived.TrySetResult(panel));
        participantConnection.On<OperatorSessionPanelDto>(
            OperatorPanelUpdatedMethod,
            panel => participantReceived.TrySetResult(panel));

        await operatorConnection.StartAsync();
        await participantConnection.StartAsync();

        await operatorConnection.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);
        await participantConnection.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var operatorClient = _factory.CreateClient();
        AddTrustedHeaders(operatorClient, OperatorExternalIdentityId, "Operator", "operator@example.com");

        var response = await operatorClient.PatchAsJsonAsync(
            $"/api/sessions/{seeded.LiveSessionId:D}/state",
            new { targetState = "Paused", reason = "Break" });

        response.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(operatorReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(operatorReceived.Task, "the assigned operator joined live-session-operators:{id}");

        var panel = await operatorReceived.Task;
        panel.LiveSessionId.Should().Be(seeded.LiveSessionId);
        panel.State.Should().Be(nameof(SessionState.Paused));
        panel.TeamProgress.Should().ContainSingle();
        panel.TeamProgress[0].TeamId.Should().Be(seeded.TeamId);
        participantReceived.Task.IsCompleted.Should().BeFalse(
            "participants only join live-session:{id}/team:{id} and must never receive operator panel updates");
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

    private async Task<SeededSession> SeedActiveTreasureHuntSessionWithDisconnectedParticipantAsync(Guid participantExternalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"HNT-{Guid.NewGuid():N}"[..12],
            "Operator Session Panel Push",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));

        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "ALP-01", 4);
        session.AssignOperator(OperatorUserId, createdAt.AddMinutes(1));
        var participant = session.AdmitParticipant(
            participantExternalIdentityId,
            "Nova",
            team.TeamId,
            createdAt.AddMinutes(1),
            new JoinPolicy()).Participant;
        session.DisconnectParticipant(participant.SessionParticipantId, createdAt.AddMinutes(2));

        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, createdAt.AddMinutes(2), transitionPolicy);
        session.MoveTo(SessionState.Active, createdAt.AddMinutes(3), transitionPolicy);

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Operator Session Panel Push",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [substage])
            ],
            [
                TargetSnapshot.Create(
                    substage.SubstageSnapshotId,
                    "Find the key",
                    "KEY-001",
                    1,
                    true,
                    100,
                    "Look near the entrance.",
                    null)
            ],
            []);
    }

    private static void AddTrustedHeaders(HttpClient client, string userId, string role, string email)
    {
        client.DefaultRequestHeaders.Remove("X-User-Id");
        client.DefaultRequestHeaders.Remove("X-User-Role");
        client.DefaultRequestHeaders.Remove("X-User-Email");
        client.DefaultRequestHeaders.Add("X-User-Id", userId);
        client.DefaultRequestHeaders.Add("X-User-Role", role);
        client.DefaultRequestHeaders.Add("X-User-Email", email);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
