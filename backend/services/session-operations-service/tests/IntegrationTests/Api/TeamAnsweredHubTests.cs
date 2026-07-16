using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

// HU-34 operator-only "team answered" transport: the signal reaches the operator-only group
// (live-session-operators:{id}) that operators join via JoinLiveSessionAsOperatorAsync, and never the
// participant-shared live-session:{id} group. Participant connections cannot join the operator group.
[Collection(PostgreSqlCollection.Name)]
public sealed class TeamAnsweredHubTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";

    private readonly PostgreSqlFixture _fixture;
    private SessionOperationsApiWebApplicationFactory _factory = null!;

    public TeamAnsweredHubTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _factory = new SessionOperationsApiWebApplicationFactory(_fixture.ConnectionString);
        _factory.EligibleTeamsClient.IsEligible = true;
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
    public async Task TeamAnswered_ReachesJoinedOperator_ButNotParticipantConnection()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveTriviaSessionAsync(externalIdentityId);

        // Operator joins the operator-only group; participant is in the participant-shared group only.
        await using var operatorConnection = CreateHubConnection(OperatorExternalIdentityId, "Operator", "operator@example.com");
        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");

        var operatorReceived = new TaskCompletionSource<TeamAnsweredNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var participantReceived = new TaskCompletionSource<TeamAnsweredNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        operatorConnection.On<TeamAnsweredNotificationDto>(
            SignalRTeamAnsweredBroadcaster.TeamAnsweredMethod,
            notification => operatorReceived.TrySetResult(notification));
        participant.On<TeamAnsweredNotificationDto>(
            SignalRTeamAnsweredBroadcaster.TeamAnsweredMethod,
            notification => participantReceived.TrySetResult(notification));

        await operatorConnection.StartAsync();
        await participant.StartAsync();

        await operatorConnection.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);
        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var notification = new TeamAnsweredNotificationDto(
            seeded.LiveSessionId,
            seeded.TeamId,
            seeded.TriviaSubstageSnapshotId,
            QuestionSequenceOrder: 1,
            AnsweredAt: DateTimeOffset.UtcNow);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var broadcaster = scope.ServiceProvider.GetRequiredService<ITeamAnsweredBroadcaster>();
            await broadcaster.BroadcastTeamAnsweredAsync(notification, CancellationToken.None);
        }

        var completed = await Task.WhenAny(operatorReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(operatorReceived.Task, "the operator joined the operator-only group");

        var received = await operatorReceived.Task;
        received.LiveSessionId.Should().Be(seeded.LiveSessionId);
        received.TeamId.Should().Be(seeded.TeamId);
        received.QuestionSequenceOrder.Should().Be(1);

        // The participant shares live-session:{id} but not the operator-only group, so it must never
        // see the answered indicator even though the broadcast already fired.
        participantReceived.Task.IsCompleted.Should().BeFalse("the answered signal must not leak to participants");
    }

    [Fact]
    public async Task JoinOperatorGroup_ByParticipantConnection_IsForbidden()
    {
        var seeded = await SeedActiveTriviaSessionAsync(Guid.NewGuid());
        await using var participant = CreateHubConnection(Guid.NewGuid().ToString(), "Participant", "participant@example.com");

        await participant.StartAsync();

        var act = () => participant.InvokeAsync(nameof(SessionsHub.JoinLiveSessionAsOperatorAsync), seeded.LiveSessionId);

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

    private async Task<SeededSession> SeedActiveTriviaSessionAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Answered Signal Quiz",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [triviaSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "What is the closest planet to the Sun?",
                    1,
                    100,
                    300,
                    "Mercury is the closest planet.",
                    [
                        TriviaOptionSnapshot.Create("Mercury", 1, true),
                        TriviaOptionSnapshot.Create("Venus", 2, false)
                    ])
            ]);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Answered Signal Session",
            45,
            createdAt,
            snapshot);

        var team = session.AssociateTeam(Guid.NewGuid(), "Red", "RED-01", 4);
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
        session.ActivateQuestion(0, createdAt.AddMinutes(3));

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId, triviaSubstage.SubstageSnapshotId);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId, Guid TriviaSubstageSnapshotId);
}
