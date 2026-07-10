using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

public sealed class TriviaRoundStartedNotificationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenTriviaSessionTransitionsToActive_BroadcastsCountdownThenActivatesNextQuestion()
    {
        var session = CreateTriviaSession();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var timerBroadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var handler = new TriviaRoundStartedNotificationHandler(
            repository.Object,
            timerBroadcaster.Object,
            facade.Object,
            new ImmediateDelayTimeProvider(Now));

        await handler.Handle(
            new SessionStateChangedEvent(session.LiveSessionId, SessionState.Preparing, SessionState.Active, Now),
            CancellationToken.None);

        timerBroadcaster.Verify(
            broadcaster => broadcaster.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(5));
        timerBroadcaster.Verify(
            broadcaster => broadcaster.BroadcastTimerUpdatedAsync(
                It.Is<SessionTimerUpdatedNotificationDto>(notification =>
                    notification.LiveSessionId == session.LiveSessionId &&
                    notification.TotalMilliseconds == 5000 &&
                    notification.SessionState == SessionState.Active.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Exactly(5));
        facade.Verify(
            current => current.ActivateNextQuestionAsync(session, Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSessionIsTreasureHunt_DoesNotBroadcastOrActivate()
    {
        var session = CreateTreasureHuntSession();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var timerBroadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var handler = new TriviaRoundStartedNotificationHandler(
            repository.Object,
            timerBroadcaster.Object,
            facade.Object,
            new ImmediateDelayTimeProvider(Now));

        await handler.Handle(
            new SessionStateChangedEvent(session.LiveSessionId, SessionState.Preparing, SessionState.Active, Now),
            CancellationToken.None);

        timerBroadcaster.Verify(
            broadcaster => broadcaster.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        facade.Verify(
            current => current.ActivateNextQuestionAsync(
                It.IsAny<LiveSession>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTransitionIsNotToActive_DoesNotQueryOrActivate()
    {
        var repository = new Mock<ILiveSessionRepository>();
        var timerBroadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var handler = new TriviaRoundStartedNotificationHandler(
            repository.Object,
            timerBroadcaster.Object,
            facade.Object,
            new ImmediateDelayTimeProvider(Now));

        await handler.Handle(
            new SessionStateChangedEvent(Guid.NewGuid(), SessionState.Active, SessionState.Paused, Now),
            CancellationToken.None);

        repository.Verify(
            repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        timerBroadcaster.Verify(
            broadcaster => broadcaster.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        facade.Verify(
            current => current.ActivateNextQuestionAsync(
                It.IsAny<LiveSession>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenQuestionAlreadyActive_DoesNotBroadcastOrActivate()
    {
        // Active trivia session that already has an active question → the `ActiveQuestionIndex is not null`
        // arm of the guard short-circuits, so the handler neither broadcasts a countdown nor re-activates.
        var session = CreateTriviaSession();
        session.ActivateQuestion(0, Now);
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var timerBroadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var handler = new TriviaRoundStartedNotificationHandler(
            repository.Object,
            timerBroadcaster.Object,
            facade.Object,
            new ImmediateDelayTimeProvider(Now));

        await handler.Handle(
            new SessionStateChangedEvent(session.LiveSessionId, SessionState.Preparing, SessionState.Active, Now),
            CancellationToken.None);

        timerBroadcaster.Verify(
            broadcaster => broadcaster.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        facade.Verify(
            current => current.ActivateNextQuestionAsync(
                It.IsAny<LiveSession>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSecondTriviaSubstageIsActive_BroadcastsCountdownThenActivatesNextQuestion()
    {
        var session = LiveSessionTestFactory.CreateScheduledMultiSubstageTrivia();
        var policy = new SessionStateTransitionPolicy();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-3), policy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-2), policy);
        session.ActivateQuestion(0, Now.AddMinutes(-2));
        session.CloseActiveQuestion(Now.AddMinutes(-1));
        session.CompleteActiveSubstageAndAdvance(Now.AddMinutes(-1), policy);
        session.ActiveQuestionIndex.Should().BeNull();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        var timerBroadcaster = new Mock<ISessionTimerBroadcaster>();
        var facade = new Mock<ITriviaRoundOrchestratorFacade>();
        var handler = new TriviaRoundStartedNotificationHandler(
            repository.Object,
            timerBroadcaster.Object,
            facade.Object,
            new ImmediateDelayTimeProvider(Now));

        await handler.Handle(
            new SessionStateChangedEvent(session.LiveSessionId, SessionState.Preparing, SessionState.Active, Now),
            CancellationToken.None);

        timerBroadcaster.Verify(
            broadcaster => broadcaster.BroadcastTimerUpdatedAsync(
                It.IsAny<SessionTimerUpdatedNotificationDto>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(5));
        facade.Verify(
            current => current.ActivateNextQuestionAsync(session, Now, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static LiveSession CreateTriviaSession()
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Trivia Session",
            45,
            1,
            Now.AddHours(1));

        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), transitionPolicy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), transitionPolicy);

        return session;
    }

    private static LiveSession CreateTreasureHuntSession()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Session",
            45,
            Now.AddHours(1));

        // Genuinely Active with a treasure-hunt first substage: the handler must PARK on the active
        // substage's play mode (D-4), not merely because the session is not yet Active.
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-2), transitionPolicy);
        session.MoveTo(SessionState.Active, Now.AddMinutes(-1), transitionPolicy);

        return session;
    }

    private sealed class ImmediateDelayTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public ImmediateDelayTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            callback(state);
            return new NoOpTimer();
        }

        private sealed class NoOpTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
