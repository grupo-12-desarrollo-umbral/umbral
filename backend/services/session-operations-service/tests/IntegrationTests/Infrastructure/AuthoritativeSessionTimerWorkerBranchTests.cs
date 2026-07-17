using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Realtime;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

public sealed class AuthoritativeSessionTimerWorkerBranchTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    // No active question means no question window to report, so the tick falls through to the mission
    // deadline — which, unlike the substage timer it replaced, is seeded for every started session and
    // so never reports a bare zero here. Broadcast-only either way: nothing expired, nothing to advance.
    [Fact]
    public async Task TickAsync_WhenSessionHasNoAdvancingQuestion_BroadcastsMissionDeadlineOnly()
    {
        var session = CreateActiveTriviaSession();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification =>
                    notification.TotalMilliseconds == (long)TimeSpan.FromMinutes(20).TotalMilliseconds &&
                    notification.RemainingMilliseconds == (long)TimeSpan.FromMinutes(19).TotalMilliseconds &&
                    !notification.IsExpired),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TickAsync_WhenQuestionTimerStillRunning_BroadcastsWithoutClosingQuestion()
    {
        var session = CreateActiveTriviaSession();
        var activatedAt = Now.AddSeconds(-5);
        session.ActivateQuestion(0, activatedAt);
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification => !notification.IsExpired && notification.RemainingMilliseconds > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // D-5: the mission deadline rides the same tick as the question window, so a trivia broadcast
    // carries both clocks. The session activated 1 minute into its 20-minute budget.
    [Fact]
    public async Task TickAsync_WhenTriviaQuestionRunning_BroadcastsMissionDeadlineAlongsideQuestionWindow()
    {
        var session = CreateActiveTriviaSession();
        session.ActivateQuestion(0, Now.AddSeconds(-5));
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification =>
                    notification.MissionTotalMilliseconds == (long)TimeSpan.FromMinutes(20).TotalMilliseconds &&
                    notification.MissionRemainingMilliseconds == (long)TimeSpan.FromMinutes(19).TotalMilliseconds &&
                    notification.RemainingMilliseconds < notification.MissionRemainingMilliseconds),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TickAsync_WhenTreasureHuntMissionTimerRunning_BroadcastsAdvancingRemaining()
    {
        var session = CreateActiveTreasureHuntSession();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        // Ticked one minute into the 45-minute mission deadline: still advancing, not expired.
        var worker = CreateWorker(repository, broadcaster, facade, Now.AddMinutes(1));

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification =>
                    !notification.IsExpired && notification.RemainingMilliseconds > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TickAsync_WhenTreasureHuntMissionTimerExpires_BroadcastsExpiredButDoesNotAdvance()
    {
        var session = CreateActiveTreasureHuntSession();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        broadcaster
            .Setup(current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        // Ticked past the 45-minute deadline: the mission timer is expired, but a treasure-hunt substage
        // advances by target resolution, not the timer — report-only, so CloseAndAdvanceAsync is never called.
        var worker = CreateWorker(repository, broadcaster, facade, Now.AddMinutes(46));

        await worker.TickAsync(CancellationToken.None);

        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification => notification.IsExpired),
                It.IsAny<CancellationToken>()),
            Times.Once);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TickAsync_WhenQuestionRevealDeadlineElapsed_CompletesRevealWithoutBroadcastingTimer()
    {
        var session = CreateActiveTriviaSession();
        session.ActivateQuestion(0, Now.AddMinutes(-1));
        // Open the reveal window with an already-past deadline (closed 1s ago, zero-length window).
        session.CloseActiveQuestionForReveal(Now.AddSeconds(-1), TimeSpan.Zero, nextQuestionIndex: null);
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        facade.Verify(
            current => current.CompleteQuestionRevealAsync(session, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        facade.Verify(
            current => current.CloseAndAdvanceAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // No timer tick is broadcast during the reveal window.
        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TickAsync_WhenQuestionRevealStillPending_DoesNotCompleteRevealOrBroadcastTimer()
    {
        var session = CreateActiveTriviaSession();
        session.ActivateQuestion(0, Now.AddMinutes(-1));
        // Reveal window still open: closed now, 30s window — the deadline has not elapsed at Now.
        session.CloseActiveQuestionForReveal(Now, TimeSpan.FromSeconds(30), nextQuestionIndex: 1);
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);
        var broadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var worker = CreateWorker(repository, broadcaster, facade, Now);

        await worker.TickAsync(CancellationToken.None);

        facade.Verify(
            current => current.CompleteQuestionRevealAsync(It.IsAny<LiveSession>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        broadcaster.Verify(
            current => current.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AuthoritativeSessionTimerWorker CreateWorker(
        Mock<ILiveSessionRepository> repository,
        Mock<ISessionTimerBroadcaster> broadcaster,
        Mock<ITriviaRoundOrchestratorFacade> facade,
        DateTimeOffset utcNow)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repository.Object);
        services.AddScoped(_ => broadcaster.Object);
        services.AddScoped(_ => facade.Object);
        var provider = services.BuildServiceProvider();

        return new AuthoritativeSessionTimerWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedTimeProvider(utcNow),
            NullLogger<AuthoritativeSessionTimerWorker>.Instance);
    }

    private static LiveSession CreateActiveTriviaSession()
    {
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Worker Trivia",
            20,
            Now.AddMinutes(-10),
            CreateTriviaSnapshot(sourceMissionId));
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), policy);
        return session;
    }

    private static LiveSession CreateActiveTreasureHuntSession()
    {
        var sourceMissionId = Guid.NewGuid();
        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Worker Treasure Hunt",
            45,
            Now.AddMinutes(-10),
            CreateTreasureHuntSnapshot(sourceMissionId));
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), policy);
        // Entering Active seeds the mission deadline from MaximumTime (45 min); the treasure hunt displays it.
        session.MoveTo(SessionState.Active, Now, policy);
        return session;
    }

    private static MissionRuntimeSnapshot CreateTreasureHuntSnapshot(Guid sourceMissionId)
    {
        var treasureHuntSubstage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);

        return MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Worker Treasure Hunt Mission",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [treasureHuntSubstage])],
            [
                TargetSnapshot.Create(
                    treasureHuntSubstage.SubstageSnapshotId,
                    "Target Alpha",
                    "QR-ALPHA",
                    1,
                    true,
                    100,
                    4.711,
                    -74.0721,
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
            "Worker Trivia Mission",
            MaximumTime.Create(20),
            [StageSnapshot.Create("Stage One", 1, [triviaSubstage])],
            [],
            [
                TriviaQuestionSnapshot.Create(
                    triviaSubstage.SubstageSnapshotId,
                    "Capital of France?",
                    1,
                    50,
                    30,
                    null,
                    [
                        TriviaOptionSnapshot.Create("Paris", 1, true),
                        TriviaOptionSnapshot.Create("Lyon", 2, false)
                    ])
            ]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
