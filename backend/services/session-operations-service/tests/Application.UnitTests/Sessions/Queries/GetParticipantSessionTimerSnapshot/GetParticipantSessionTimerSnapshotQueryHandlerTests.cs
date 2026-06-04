using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetParticipantSessionTimerSnapshot;

public sealed class GetParticipantSessionTimerSnapshotQueryHandlerTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenTimerIsActive_ReturnsAuthoritativeRemainingTime()
    {
        var session = CreateActiveSession(activeAt: StartsAt.AddMinutes(1), maximumTimeMinutes: 10);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(
            session,
            teamId,
            isAllowed: true,
            observedAt: StartsAt.AddMinutes(4));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, "join-token"),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(teamId);
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
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(
            session,
            teamId,
            isAllowed: true,
            observedAt: StartsAt.AddMinutes(20));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
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
        var teamId = session.Teams.Single().TeamId;
        session.MoveTo(SessionState.Active, StartsAt.AddMinutes(15), new SessionStateTransitionPolicy());
        var handler = CreateHandler(
            session,
            teamId,
            isAllowed: true,
            observedAt: StartsAt.AddMinutes(17));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.RemainingSeconds.Should().Be(240);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.AdvancingSince.Should().Be(StartsAt.AddMinutes(15));
    }

    [Fact]
    public async Task Handle_WhenTimerElapsed_ReturnsExpiredStatusWithoutClientState()
    {
        var session = CreateActiveSession(activeAt: StartsAt.AddMinutes(1), maximumTimeMinutes: 10);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(
            session,
            teamId,
            isAllowed: true,
            observedAt: StartsAt.AddMinutes(12));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.RemainingSeconds.Should().Be(0);
        result.TimerStatus.Should().Be("Expired");
        result.IsAdvancing.Should().BeFalse();
        result.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenMembershipAccessIsDenied_ThrowsForbidden()
    {
        var session = CreateActiveSession(activeAt: StartsAt.AddMinutes(1), maximumTimeMinutes: 10);
        var teamId = session.Teams.Single().TeamId;
        var repository = CreateRepository(session);
        var handler = CreateHandler(
            repository,
            CreateAccessClient(session.LiveSessionId, teamId, isAllowed: false),
            StartsAt.AddMinutes(2));

        var act = async () => await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLiveSessionDoesNotExist_ThrowsNotFound()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var handler = CreateHandler(
            repository,
            CreateAccessClient(liveSessionId, teamId, isAllowed: true),
            StartsAt.AddMinutes(2));

        var act = async () => await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(liveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GetParticipantSessionTimerSnapshotQueryHandler CreateHandler(
        LiveSession session,
        Guid teamId,
        bool isAllowed,
        DateTimeOffset observedAt)
    {
        return CreateHandler(
            CreateRepository(session),
            CreateAccessClient(session.LiveSessionId, teamId, isAllowed),
            observedAt);
    }

    private static GetParticipantSessionTimerSnapshotQueryHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IParticipantMembershipAccessClient> accessClient,
        DateTimeOffset observedAt)
    {
        return new GetParticipantSessionTimerSnapshotQueryHandler(
            repository.Object,
            accessClient.Object,
            new FixedTimeProvider(observedAt));
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return repository;
    }

    private static Mock<IParticipantMembershipAccessClient> CreateAccessClient(
        Guid liveSessionId,
        Guid teamId,
        bool isAllowed)
    {
        var accessClient = new Mock<IParticipantMembershipAccessClient>();
        accessClient
            .Setup(client => client.ValidateAsync(liveSessionId, teamId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantMembershipAccessDecisionDto(
                "ParticipantExperience",
                isAllowed,
                isAllowed ? "allowed" : "denied",
                liveSessionId,
                teamId));

        return accessClient;
    }

    private static LiveSession CreateActiveSession(DateTimeOffset activeAt, int maximumTimeMinutes)
    {
        var session = LiveSession.Create(
            SessionMode.TreasureHunt,
            SessionSource.Create(SessionSourceType.Mission, Guid.NewGuid()),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Timer Session",
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
