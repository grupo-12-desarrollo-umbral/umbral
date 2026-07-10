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

namespace umbral_backend.Infrastructure.IntegrationTests.Api;

/// <summary>
/// Pins the real-time side of a state transition: a valid transition pushes a "SessionStateChanged"
/// message carrying the previous and new state to the live-session:{id} group, so only members who
/// joined that session's group receive it. Also covers timer and question broadcasts over the same hub.
/// </summary>
[Collection(PostgreSqlCollection.Name)]
public sealed class SessionStateBroadcastHubTests : IAsyncLifetime
{
    private const int OperatorUserId = 55;
    private const string OperatorExternalIdentityId = "kc-operator-55";
    private const string SecondSubstageQuestionPrompt = "Substage two question?";

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

    // HU-22 transport gate: the SessionTimerUpdated broadcast carries the active-substage
    // (trivia-question) remaining time and reaches the live-session:{id} group. The payload is derived
    // from the session's authoritative timer window, not a whole-session countdown.
    [Fact]
    public async Task TimerBroadcaster_BroadcastsSubstageDerivedSessionTimerUpdatedToConnectedParticipant()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedPausedTriviaSessionWithDisconnectedParticipantAsync(externalIdentityId);

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
        SessionTimerUpdatedNotificationDto broadcast;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // Derive the broadcast payload from the session's active-substage timer window.
            var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            var snapshot = session!.GetAuthoritativeSessionTimerSnapshot(emittedAt);

            broadcast = new SessionTimerUpdatedNotificationDto(
                seeded.LiveSessionId,
                RemainingMilliseconds: (long)Math.Ceiling(snapshot.RemainingDuration.TotalMilliseconds),
                IsPaused: session.State == SessionState.Paused,
                EmittedAt: emittedAt,
                TotalMilliseconds: (long)Math.Ceiling(snapshot.TotalDuration.TotalMilliseconds),
                IsExpired: snapshot.IsExpired,
                SessionState: session.State.ToString());

            var broadcaster = scope.ServiceProvider.GetRequiredService<ISessionTimerBroadcaster>();
            await broadcaster.BroadcastTimerUpdatedAsync(broadcast, CancellationToken.None);
        }

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task, "timer updates must broadcast over SignalR");

        var notification = await received.Task;
        notification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        notification.IsPaused.Should().BeTrue();
        notification.EmittedAt.Should().BeCloseTo(emittedAt, TimeSpan.FromSeconds(1));
        // The frozen active-question remainder (30s window paused at 10s) = 20s, proving the payload
        // is the substage-derived window and not the deleted whole-session countdown.
        notification.RemainingMilliseconds.Should().Be(20_000);
        notification.RemainingMilliseconds.Should().Be(broadcast.RemainingMilliseconds);
        notification.TotalMilliseconds.Should().Be(30_000);
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

    // HU-33A X.4 transport gate: a real facade-driven trivia round pushes the full question lifecycle
    // to the live-session:{id} group — QuestionClosed (the active substage's last question) ->
    // SubstageAdvanced (the boundary, carrying from/to substage + play mode) -> QuestionActivated (the
    // next substage's first question) — and the timer read then exposes that next active-substage
    // question, proving the synchronized broadcasts and the substage-scoped read agree.
    [Fact]
    public async Task TriviaRound_ClosingLastQuestion_BroadcastsSubstageAdvancedAndActivatesNextSubstage()
    {
        var externalIdentityId = Guid.NewGuid();
        var seeded = await SeedActiveTwoSubstageTriviaSessionAsync(externalIdentityId);

        await using var participant = CreateHubConnection(externalIdentityId.ToString(), "Participant", "participant@example.com");
        await participant.StartAsync();
        await participant.InvokeAsync(
            nameof(SessionsHub.ReconnectAsync),
            seeded.LiveSessionId,
            new SessionsHub.ReconnectParticipantHubRequest(seeded.TeamId, "Nova", null));

        var closed = new TaskCompletionSource<QuestionClosedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var advanced = new TaskCompletionSource<SubstageAdvancedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var activated = new TaskCompletionSource<QuestionActivatedNotificationDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        participant.On<QuestionClosedNotificationDto>(
            SignalRSessionQuestionBroadcaster.QuestionClosedMethod,
            notification => closed.TrySetResult(notification));
        participant.On<SubstageAdvancedNotificationDto>(
            SignalRSessionQuestionBroadcaster.SubstageAdvancedMethod,
            notification => advanced.TrySetResult(notification));
        participant.On<QuestionActivatedNotificationDto>(
            SignalRSessionQuestionBroadcaster.QuestionActivatedMethod,
            notification => activated.TrySetResult(notification));

        // Drive the authoritative round the way the timer worker does on last-question expiry: the
        // facade closes the active substage's last question, advances the substage, and activates the
        // next substage's first question.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
            var facade = scope.ServiceProvider.GetRequiredService<ITriviaRoundOrchestratorFacade>();
            var session = await repository.GetByIdAsync(seeded.LiveSessionId, CancellationToken.None);
            await facade.CloseAndAdvanceAsync(session!, DateTimeOffset.UtcNow, CancellationToken.None);
        }

        (await AwaitBroadcast(closed.Task)).QuestionIndex.Should().Be(0);

        var advancedNotification = await AwaitBroadcast(advanced.Task);
        advancedNotification.LiveSessionId.Should().Be(seeded.LiveSessionId);
        advancedNotification.FromSubstageId.Should().Be(seeded.FirstSubstageId);
        advancedNotification.ToSubstageId.Should().Be(seeded.SecondSubstageId);
        advancedNotification.FromPlayMode.Should().Be(nameof(SubstagePlayMode.Trivia));

        (await AwaitBroadcast(activated.Task)).Prompt.Should().Be(SecondSubstageQuestionPrompt);

        // The substage-scoped timer read now exposes the NEW active substage's question (not the
        // closed substage-one question), so the broadcast round and the read agree.
        var operatorClient = _factory.CreateClient();
        operatorClient.DefaultRequestHeaders.Add("X-User-Id", OperatorExternalIdentityId);
        operatorClient.DefaultRequestHeaders.Add("X-User-Role", "Operator");
        operatorClient.DefaultRequestHeaders.Add("X-User-Email", "operator@example.com");

        var timerResponse = await operatorClient.GetAsync($"/api/sessions/{seeded.LiveSessionId:D}/timer");
        timerResponse.EnsureSuccessStatusCode();
        var timer = await timerResponse.Content.ReadFromJsonAsync<TimerReadResponse>();
        timer!.ActiveQuestion.Should().NotBeNull();
        timer.ActiveQuestion!.Prompt.Should().Be(SecondSubstageQuestionPrompt);
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

    private static async Task<T> AwaitBroadcast<T>(Task<T> received)
    {
        var completed = await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received, "the round lifecycle must broadcast over SignalR");
        return await received;
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
        // Activate a trivia question then pause 10s later -> frozen active-substage remainder = 30 - 10 = 20s.
        session.ActivateQuestion(0, createdAt.AddMinutes(3));
        session.MoveTo(SessionState.Paused, createdAt.AddMinutes(3).AddSeconds(10), transitionPolicy, "Trivia test");

        dbContext.LiveSessions.Add(session);
        await dbContext.SaveChangesAsync();

        return new SeededSession(session.LiveSessionId, team.TeamId);
    }

    // An all-trivia, two-substage mission in Active with the first substage's only question already
    // activated, so the round-start handler returns early (index set) and the test drives the
    // close-and-advance itself.
    private async Task<SeededTriviaRound> SeedActiveTwoSubstageTriviaSessionAsync(Guid externalIdentityId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        var sourceMissionId = Guid.NewGuid();
        var firstSubstage = SubstageSnapshot.CreateTrivia("Trivia Round One", 1);
        var secondSubstage = SubstageSnapshot.CreateTrivia("Trivia Round Two", 2);
        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Two Substage Trivia Mission",
            MaximumTime.Create(20),
            [
                StageSnapshot.Create("Stage One", 1, [firstSubstage, secondSubstage])
            ],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    firstSubstage.SubstageSnapshotId,
                    "Substage one question?",
                    1,
                    50,
                    30,
                    "Explanation one.",
                    [
                        TriviaOptionSnapshot.Create("A", 1, true),
                        TriviaOptionSnapshot.Create("B", 2, false)
                    ]),
                TriviaQuestionSnapshot.Create(
                    secondSubstage.SubstageSnapshotId,
                    SecondSubstageQuestionPrompt,
                    1,
                    50,
                    30,
                    "Explanation two.",
                    [
                        TriviaOptionSnapshot.Create("C", 1, true),
                        TriviaOptionSnapshot.Create("D", 2, false)
                    ])
            ]);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Two Substage Trivia Session",
            20,
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

        return new SeededTriviaRound(
            session.LiveSessionId,
            team.TeamId,
            firstSubstage.SubstageSnapshotId,
            secondSubstage.SubstageSnapshotId);
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

    private sealed record SeededTriviaRound(
        Guid LiveSessionId,
        Guid TeamId,
        Guid FirstSubstageId,
        Guid SecondSubstageId);

    // Minimal projection of the operator timer read — extra fields are ignored on deserialize.
    private sealed record TimerReadResponse(Guid LiveSessionId, ActiveQuestionRead? ActiveQuestion);

    private sealed record ActiveQuestionRead(string Prompt);
}
