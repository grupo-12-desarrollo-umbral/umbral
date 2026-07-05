using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetOperatorSessionTimerSnapshot;

// Operator timer query returns the active-substage (trivia-question) window (HU-22): remaining
// tracks the active question, and is 0 with no ActiveQuestion when no question is active.
public sealed class GetOperatorSessionTimerSnapshotQueryHandlerTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivatedAt = StartsAt.AddMinutes(2);

    [Fact]
    public async Task Handle_WhenTriviaQuestionIsActive_ReturnsActiveSubstageRemaining()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var handler = CreateHandler(session, observedAt: ActivatedAt.AddSeconds(5));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.TeamId.Should().BeNull();
        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.TotalSeconds.Should().Be(30);
        result.RemainingSeconds.Should().Be(25);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.IsExpired.Should().BeFalse();
        result.AdvancingSince.Should().Be(ActivatedAt);

        result.ActiveQuestion.Should().NotBeNull();
        result.ActiveQuestion!.QuestionIndex.Should().Be(0);
        result.ActiveQuestion.SequenceOrder.Should().Be(1);
        result.ActiveQuestion.Prompt.Should().Be("What is the closest planet to the Sun?");
        result.ActiveQuestion.Options.Should().Equal("Mercury", "Venus");
        result.ActiveQuestion.TimeLimitSeconds.Should().Be(30);
        result.ActiveQuestion.RemainingSeconds.Should().Be(25);
        result.ActiveQuestion.ActivatedAt.Should().Be(ActivatedAt);
    }

    [Fact]
    public async Task Handle_WhenNoQuestionIsActive_ReturnsNoCountdownAndNoActiveQuestion()
    {
        var session = CreateActiveTreasureHunt(activeAt: StartsAt.AddMinutes(1));
        var handler = CreateHandler(session, observedAt: StartsAt.AddMinutes(4));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.TotalSeconds.Should().Be(0);
        result.RemainingSeconds.Should().Be(0);
        result.IsAdvancing.Should().BeFalse();
        result.ActiveQuestion.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPaused_FreezesActiveQuestionRemainder()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        session.MoveTo(SessionState.Paused, ActivatedAt.AddSeconds(10), new SessionStateTransitionPolicy());
        var handler = CreateHandler(session, observedAt: ActivatedAt.AddSeconds(50));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Paused));
        result.RemainingSeconds.Should().Be(20);
        result.TimerStatus.Should().Be("Frozen");
        result.IsAdvancing.Should().BeFalse();
        result.AdvancingSince.Should().BeNull();

        result.ActiveQuestion.Should().NotBeNull();
        result.ActiveQuestion!.RemainingSeconds.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WhenResumed_ContinuesSameQuestionAtFrozenRemainder()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Paused, ActivatedAt.AddSeconds(10), policy);
        session.MoveTo(SessionState.Active, ActivatedAt.AddSeconds(60), policy);
        var handler = CreateHandler(session, observedAt: ActivatedAt.AddSeconds(65));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.RemainingSeconds.Should().Be(15);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.AdvancingSince.Should().Be(ActivatedAt.AddSeconds(60));
        result.ActiveQuestion!.RemainingSeconds.Should().Be(15);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_PropagatesNotFoundException()
    {
        var liveSessionId = Guid.NewGuid();
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(r => r.GetAuthorizedTimerSessionAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(nameof(LiveSession), liveSessionId));
        var handler = new GetOperatorSessionTimerSnapshotQueryHandler(
            resolver.Object,
            new FixedTimeProvider(StartsAt));

        var act = async () => await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(liveSessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static GetOperatorSessionTimerSnapshotQueryHandler CreateHandler(
        LiveSession session,
        DateTimeOffset observedAt)
    {
        var resolver = new Mock<ISessionAdministrationAccessResolver>();
        resolver
            .Setup(r => r.GetAuthorizedTimerSessionAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return new GetOperatorSessionTimerSnapshotQueryHandler(
            resolver.Object,
            new FixedTimeProvider(observedAt));
    }

    private static LiveSession CreateActiveTriviaSession(DateTimeOffset activatedAt)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(scheduledAt: StartsAt);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, activatedAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, activatedAt.AddSeconds(-1), policy);
        session.ActivateQuestion(0, activatedAt);

        return session;
    }

    private static LiveSession CreateActiveTreasureHunt(DateTimeOffset activeAt)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(scheduledAt: StartsAt);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, activeAt, policy);

        return session;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
