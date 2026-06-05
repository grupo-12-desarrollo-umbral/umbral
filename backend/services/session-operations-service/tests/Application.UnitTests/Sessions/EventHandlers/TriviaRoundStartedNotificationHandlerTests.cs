using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Application.Sessions.Facades;
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

    private static LiveSession CreateTriviaSession()
    {
        return LiveSession.CreateTrivia(
            SessionSource.CreateTriviaQuiz(42),
            $"TRV-{Guid.NewGuid():N}"[..12],
            "Trivia Session",
            45,
            Now.AddHours(1),
            TriviaSessionSnapshot.Create(
                "Quiz",
                [
                    TriviaQuestionSnapshot.Create(
                        "Question 1",
                        1,
                        100,
                        30,
                        null,
                        [
                            TriviaOptionSnapshot.Create("Option 1A", 1, true),
                            TriviaOptionSnapshot.Create("Option 1B", 2, false)
                        ])
                ]));
    }

    private static LiveSession CreateTreasureHuntSession()
    {
        return LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Treasure Session",
            45,
            Now.AddHours(1));
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
