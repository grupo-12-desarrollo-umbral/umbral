using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Api.Hubs;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;
using umbral_backend.Infrastructure.Realtime;

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
    public async Task TimerBroadcaster_BroadcastsSessionTimerUpdatedToConnectedParticipant()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedPausedSessionWithDisconnectedParticipantAsync(externalIdentityId);

        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");
        await participant.StartAsync();

        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var received = new TaskCompletionSource<SessionTimerUpdatedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        participant.On<SessionTimerUpdatedNotificationDto>(
            SignalRSessionTimerBroadcaster.TimerUpdatedMethod,
            notification => received.TrySetResult(notification));

        var emittedAt = DateTimeOffset.UtcNow;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var broadcaster = scope.ServiceProvider.GetRequiredService<ISessionTimerBroadcaster>();
            await broadcaster.BroadcastTimerUpdatedAsync(
                new SessionTimerUpdatedNotificationDto(
                    seeded.LiveSessionId,
                    RemainingMilliseconds: 120_000,
                    IsPaused: true,
                    EmittedAt: emittedAt,
                    TotalMilliseconds: 2_700_000,
                    IsExpired: false,
                    SessionState: nameof(SessionState.Paused)),
                CancellationToken.None);
        }

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task, "timer updates must broadcast over SignalR");

        var notification = await received.Task;
        notification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        notification.RemainingMilliseconds.Should().Be(120_000);
        notification.IsPaused.Should().BeTrue();
        notification.EmittedAt.Should().BeCloseTo(emittedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task QuestionBroadcaster_BroadcastsQuestionLifecycleEventsToConnectedParticipant()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedPausedTriviaSessionWithDisconnectedParticipantAsync(externalIdentityId);

        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");
        await participant.StartAsync();

        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var activated = new TaskCompletionSource<QuestionActivatedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource<QuestionClosedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        participant.On<QuestionActivatedNotificationDto>(
            SignalRSessionQuestionBroadcaster.QuestionActivatedMethod,
            notification => activated.TrySetResult(notification));
        participant.On<QuestionClosedNotificationDto>(
            SignalRSessionQuestionBroadcaster.QuestionClosedMethod,
            notification => closed.TrySetResult(notification));

        var activatedAt = DateTimeOffset.UtcNow;
        var closedAt = activatedAt.AddSeconds(30);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var broadcaster = scope.ServiceProvider.GetRequiredService<ISessionQuestionBroadcaster>();
            await broadcaster.BroadcastQuestionActivatedAsync(
                new QuestionActivatedNotificationDto(
                    seeded.LiveSessionId,
                    QuestionIndex: 0,
                    SequenceOrder: 1,
                    Prompt: "Capital of France?",
                    Options: ["Paris", "Lyon"],
                    TimeLimitSeconds: 30,
                    ActivatedAt: activatedAt),
                CancellationToken.None);

            await broadcaster.BroadcastQuestionClosedAsync(
                new QuestionClosedNotificationDto(
                    seeded.LiveSessionId,
                    QuestionIndex: 0,
                    ClosedAt: closedAt,
                    WasExpiredByTimer: true),
                CancellationToken.None);
        }

        var activatedCompleted = await Task.WhenAny(activated.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        activatedCompleted.Should().Be(activated.Task, "question activation must broadcast over SignalR");

        var closedCompleted = await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        closedCompleted.Should().Be(closed.Task, "question closure must broadcast over SignalR");

        (await activated.Task).Prompt.Should().Be("Capital of France?");
        (await closed.Task).WasExpiredByTimer.Should().BeTrue();
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

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Broadcast Session",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
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

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private async Task<SeededSession> SeedPausedSessionWithDisconnectedParticipantAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Paused Timer Session",
            45,
            createdAt,
            CreateTreasureHuntSnapshot(sourceMissionId));
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
        session.MoveTo(SessionState.Paused, createdAt.AddMinutes(4), transitionPolicy, "Timer test");

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private async Task<SeededSession> SeedPausedTriviaSessionWithDisconnectedParticipantAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Paused Trivia Session",
            20,
            createdAt,
            CreateTriviaSnapshot(sourceMissionId));

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
        session.MoveTo(SessionState.Paused, createdAt.AddMinutes(4), transitionPolicy, "Trivia test");

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Hunt", 1, 100);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Seeded Mission",
            MaximumTime.Create(45),
            [
                StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])
            ],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    "Look under the stairs",
                    "AfterPreviousTarget")
            ],
            []);
    }

    private static MissionRuntimeSnapshot CreateTriviaSnapshot(Guid sourceMissionId)
    {
        var triviaSubstage = SubstageSnapshot.CreateTrivia("Trivia Round", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Seeded Trivia Mission",
            MaximumTime.Create(20),
            [
                StageSnapshot.Create("Stage One", 1, [triviaSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of France?",
                    1,
                    50,
                    30,
                    "Paris is the capital city.",
                    [
                        TriviaOptionSnapshot.Create("Paris", 1, true),
                        TriviaOptionSnapshot.Create("Lyon", 2, false)
                    ])
            ]);
    }

    private sealed record SeededSession(Guid LiveSessionId, Guid TeamId);
}
