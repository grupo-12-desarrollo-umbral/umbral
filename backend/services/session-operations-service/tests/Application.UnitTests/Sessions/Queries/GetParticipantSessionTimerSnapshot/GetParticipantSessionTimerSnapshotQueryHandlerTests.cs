using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetParticipantSessionTimerSnapshot;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetParticipantSessionTimerSnapshot;

// Participant timer query returns the authoritative active-substage window under the unchanged
// membership guard: trivia exposes its active question, while treasure hunt exposes the session-level
// countdown without an ActiveQuestion; denied and missing reads still throw.
public sealed class GetParticipantSessionTimerSnapshotQueryHandlerTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivatedAt = StartsAt.AddMinutes(2);

    [Fact]
    public async Task Handle_WhenTriviaQuestionIsActive_ReturnsActiveSubstageRemaining()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: ActivatedAt.AddSeconds(5));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, "join-token"),
            CancellationToken.None);

        result.TeamId.Should().Be(teamId);
        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.TotalSeconds.Should().Be(30);
        result.RemainingSeconds.Should().Be(25);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.AdvancingSince.Should().Be(ActivatedAt);

        result.ActiveQuestion.Should().NotBeNull();
        result.ActiveQuestion!.QuestionIndex.Should().Be(0);
        result.ActiveQuestion.TimeLimitSeconds.Should().Be(30);
        result.ActiveQuestion.RemainingSeconds.Should().Be(25);
    }

    [Fact]
    public async Task Handle_WhenTreasureHuntSubstageIsActive_ReturnsAdvancingCountdownWithoutActiveQuestion()
    {
        var session = CreateActiveTreasureHunt(activeAt: StartsAt.AddMinutes(1));
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: StartsAt.AddMinutes(4));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Active));
        result.TotalSeconds.Should().Be(45 * 60);
        result.RemainingSeconds.Should().Be(42 * 60);
        result.TimerStatus.Should().Be("Advancing");
        result.IsAdvancing.Should().BeTrue();
        result.IsExpired.Should().BeFalse();
        result.AdvancingSince.Should().Be(StartsAt.AddMinutes(1));
        result.ActiveQuestion.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPaused_FreezesActiveQuestionRemainder()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        session.MoveTo(SessionState.Paused, ActivatedAt.AddSeconds(10), new SessionStateTransitionPolicy());
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: ActivatedAt.AddSeconds(50));

        var result = await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.SessionState.Should().Be(nameof(SessionState.Paused));
        result.RemainingSeconds.Should().Be(20);
        result.TimerStatus.Should().Be("Frozen");
        result.IsAdvancing.Should().BeFalse();
        result.ActiveQuestion!.RemainingSeconds.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WhenMembershipAccessIsDenied_ThrowsForbidden()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var repository = CreateRepository(session);
        var handler = CreateHandler(
            repository,
            CreateGuard(session.LiveSessionId, teamId, isAllowed: false),
            ActivatedAt.AddSeconds(5));

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
            CreateGuard(liveSessionId, teamId, isAllowed: true),
            StartsAt.AddMinutes(2));

        var act = async () => await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(liveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAuthenticatedUserIsNotTeamMember_ThrowsForbidden()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(
            CreateRepository(session),
            CreateGuard(session.LiveSessionId, teamId, isAllowed: true),
            ActivatedAt.AddSeconds(5),
            CreateChecker(isMember: false));

        var act = async () => await handler.Handle(
            new GetParticipantSessionTimerSnapshotQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static GetParticipantSessionTimerSnapshotQueryHandler CreateHandler(
        LiveSession session,
        Guid teamId,
        bool isAllowed,
        DateTimeOffset observedAt)
    {
        return CreateHandler(
            CreateRepository(session),
            CreateGuard(session.LiveSessionId, teamId, isAllowed),
            observedAt);
    }

    private static GetParticipantSessionTimerSnapshotQueryHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IRuntimeParticipationGuard> guard,
        DateTimeOffset observedAt,
        Mock<IParticipantSessionMembershipChecker>? membershipChecker = null)
    {
        return new GetParticipantSessionTimerSnapshotQueryHandler(
            repository.Object,
            guard.Object,
            (membershipChecker ?? CreateChecker(isMember: true)).Object,
            new FixedTimeProvider(observedAt));
    }

    private static Mock<IParticipantSessionMembershipChecker> CreateChecker(bool isMember)
    {
        var checker = new Mock<IParticipantSessionMembershipChecker>();
        checker
            .Setup(c => c.Check(It.IsAny<LiveSession>(), It.IsAny<Guid>()))
            .Returns(isMember
                ? ParticipantSessionMembershipResult.Allowed
                : ParticipantSessionMembershipResult.Deny("participant-not-assigned-to-team"));

        return checker;
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return repository;
    }

    private static Mock<IRuntimeParticipationGuard> CreateGuard(
        Guid liveSessionId,
        Guid teamId,
        bool isAllowed)
    {
        var guard = new Mock<IRuntimeParticipationGuard>();
        var setup = guard.Setup(g => g.EnsureAllowedAsync(
            liveSessionId,
            teamId,
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()));

        if (isAllowed)
        {
            setup.Returns(Task.CompletedTask);
        }
        else
        {
            setup.ThrowsAsync(new ForbiddenAccessException());
        }

        return guard;
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
