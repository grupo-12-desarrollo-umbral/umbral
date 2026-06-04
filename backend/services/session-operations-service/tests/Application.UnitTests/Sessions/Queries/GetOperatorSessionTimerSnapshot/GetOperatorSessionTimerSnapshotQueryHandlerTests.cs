using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Application.Sessions.Queries.GetOperatorSessionTimerSnapshot;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetOperatorSessionTimerSnapshot;

public sealed class GetOperatorSessionTimerSnapshotQueryHandlerTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenTimerIsActive_ReturnsAdvancingSnapshotWithNullTeamId()
    {
        var session = CreateActiveSession(activeAt: StartsAt.AddMinutes(1), maximumTimeMinutes: 10);
        var handler = CreateHandler(session, observedAt: StartsAt.AddMinutes(4));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().BeNull();
        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.TotalSeconds.Should().Be(600);
        result.RemainingSeconds.Should().Be(420);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.IsExpired.Should().BeFalse();
        result.AdvancingSince.Should().Be(StartsAt.AddMinutes(1));
    }

    [Fact]
    public async Task Handle_WhenTimerIsPaused_ReturnsFrozenSnapshot()
    {
        var session = CreatePausedSession();
        var handler = CreateHandler(session, observedAt: StartsAt.AddMinutes(20));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Paused));
        result.RemainingSeconds.Should().Be(360);
        result.TimerStatus.Should().Be("Frozen");
        result.IsAdvancing.Should().BeFalse();
        result.IsExpired.Should().BeFalse();
        result.AdvancingSince.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTimerWasResumed_ReturnsRemainderFromFrozenTime()
    {
        var session = CreatePausedSession();
        session.MoveTo(SessionState.Active, StartsAt.AddMinutes(15), new SessionStateTransitionPolicy());
        var handler = CreateHandler(session, observedAt: StartsAt.AddMinutes(17));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.RemainingSeconds.Should().Be(240);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.AdvancingSince.Should().Be(StartsAt.AddMinutes(15));
    }

    [Fact]
    public async Task Handle_WhenTimerElapsed_ReturnsExpiredSnapshot()
    {
        var session = CreateActiveSession(activeAt: StartsAt.AddMinutes(1), maximumTimeMinutes: 10);
        var handler = CreateHandler(session, observedAt: StartsAt.AddMinutes(12));

        var result = await handler.Handle(
            new GetOperatorSessionTimerSnapshotQuery(session.LiveSessionId),
            CancellationToken.None);

        result.RemainingSeconds.Should().Be(0);
        result.TimerStatus.Should().Be("Expired");
        result.IsAdvancing.Should().BeFalse();
        result.IsExpired.Should().BeTrue();
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

    private static LiveSession CreateActiveSession(DateTimeOffset activeAt, int maximumTimeMinutes)
    {
        var session = LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Operator Timer Session",
            maximumTimeMinutes,
            StartsAt);

        session.RegisterTeam("Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, activeAt.AddMinutes(-1), transitionPolicy);
        session.MoveTo(SessionState.Active, activeAt, transitionPolicy);

        return session;
    }

    private static LiveSession CreatePausedSession()
    {
        var session = CreateActiveSession(StartsAt.AddMinutes(1), maximumTimeMinutes: 10);
        session.MoveTo(SessionState.Paused, StartsAt.AddMinutes(5), new SessionStateTransitionPolicy());
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
