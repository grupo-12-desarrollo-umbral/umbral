using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetParticipantTeamBoard;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Queries.GetParticipantTeamBoard;

public sealed class GetParticipantTeamBoardQueryHandlerTests
{
    private static readonly DateTimeOffset StartsAt = new(2026, 6, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivatedAt = StartsAt.AddMinutes(2);

    [Fact]
    public async Task Handle_CallsRuntimeGuardBeforeReturningData()
    {
        var session = CreateActiveTreasureHuntSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var guard = CreateGuard(session.LiveSessionId, teamId, isAllowed: true);
        var repository = CreateRepository(session);
        var handler = CreateHandler(repository, guard, ActivatedAt.AddSeconds(5));

        await handler.Handle(
            new GetParticipantTeamBoardQuery(session.LiveSessionId, teamId, "token"),
            CancellationToken.None);

        guard.Verify(g => g.EnsureAllowedAsync(
            session.LiveSessionId,
            It.IsAny<CancellationToken>()), Times.Once);

        guard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForTreasureHuntSession_MapsScoreTimerProgressAndClues()
    {
        var session = CreateActiveTreasureHuntSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: ActivatedAt.AddSeconds(5));

        var result = await handler.Handle(
            new GetParticipantTeamBoardQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(teamId);
        result.TeamDisplayName.Should().Be("Alpha");
        result.TeamCode.Should().Be("A-01");
        result.CurrentScore.Should().Be(0);
        result.Timer.Should().NotBeNull();
        result.Timer.SessionState.Should().Be(nameof(SessionState.Active));
        result.ActiveSubstage.Should().NotBeNull();
        result.ActiveSubstage!.PlayMode.Should().Be(nameof(SubstagePlayMode.TreasureHunt));
        result.ActiveSubstage.TotalActiveTargets.Should().Be(1);
        result.ActiveSubstage.ResolvedTargets.Should().Be(0);
        result.VisibleClues.Should().HaveCount(1);
        result.VisibleClues[0].ClueText.Should().Be("Look near the entrance.");
        result.VisibleClues[0].OperativeClueId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ForTriviaSession_MapsActiveQuestionContext()
    {
        var session = CreateActiveTriviaSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(session, teamId, isAllowed: true, observedAt: ActivatedAt.AddSeconds(5));

        var result = await handler.Handle(
            new GetParticipantTeamBoardQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        result.ActiveSubstage.Should().NotBeNull();
        result.ActiveSubstage!.PlayMode.Should().Be(nameof(SubstagePlayMode.Trivia));
        result.ActiveSubstage.ActiveQuestionSequenceOrder.Should().NotBeNull();
        result.ActiveSubstage.ActiveQuestionTimeLimitSeconds.Should().Be(30);
        result.ActiveSubstage.TotalActiveTargets.Should().Be(0);
        result.ActiveSubstage.ResolvedTargets.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenMembershipAccessIsDenied_ThrowsForbidden()
    {
        var session = CreateActiveTreasureHuntSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var repository = CreateRepository(session);
        var handler = CreateHandler(
            repository,
            CreateGuard(session.LiveSessionId, teamId, isAllowed: false),
            ActivatedAt.AddSeconds(5));

        var act = async () => await handler.Handle(
            new GetParticipantTeamBoardQuery(session.LiveSessionId, teamId, null),
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
            new GetParticipantTeamBoardQuery(liveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAuthenticatedUserIsNotTeamMember_ThrowsForbidden()
    {
        var session = CreateActiveTreasureHuntSession(ActivatedAt);
        var teamId = session.Teams.Single().TeamId;
        var handler = CreateHandler(
            CreateRepository(session),
            CreateGuard(session.LiveSessionId, teamId, isAllowed: true),
            ActivatedAt.AddSeconds(5),
            CreateChecker(isMember: false));

        var act = async () => await handler.Handle(
            new GetParticipantTeamBoardQuery(session.LiveSessionId, teamId, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static GetParticipantTeamBoardQueryHandler CreateHandler(
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

    private static GetParticipantTeamBoardQueryHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IRuntimeParticipationGuard> guard,
        DateTimeOffset observedAt,
        Mock<IParticipantSessionMembershipChecker>? membershipChecker = null)
    {
        return new GetParticipantTeamBoardQueryHandler(
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
            It.IsAny<CancellationToken>()));

        if (isAllowed)
        {
            setup.ReturnsAsync(new ParticipantEligibleTeamsDto(true, "eligible", []));
        }
        else
        {
            setup.ThrowsAsync(new ForbiddenAccessException());
        }

        return guard;
    }

    private static LiveSession CreateActiveTreasureHuntSession(DateTimeOffset activatedAt)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt(scheduledAt: StartsAt);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, activatedAt.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, activatedAt, policy);

        return session;
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
