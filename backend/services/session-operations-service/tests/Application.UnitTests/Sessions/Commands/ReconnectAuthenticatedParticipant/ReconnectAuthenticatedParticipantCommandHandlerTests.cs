using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenDisconnectedParticipantReturnsToAssignedTeam_ReconnectsAndReturnsRuntimeContext()
    {
        var session = CreateScheduledSession();
        var identityReferenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(identityReferenceTeamId, "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var firstAdmission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new JoinPolicy());
        session.DisconnectParticipant(
            firstAdmission.Participant.SessionParticipantId,
            new DateTimeOffset(2026, 6, 3, 10, 6, 0, TimeSpan.Zero));

        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", "join-token");
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero)));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsReconnect.Should().BeTrue();
        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(team.TeamId);
        result.TeamDisplayName.Should().Be("Alpha");
        result.ParticipantDisplayName.Should().Be("Nora");
        result.SessionState.Should().Be(SessionState.Scheduled.ToString());
        result.LastSeenAt.Should().Be(new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero));
        // HU-22: no active trivia question → no active-substage countdown (0 / no ActiveQuestion).
        result.Timer.Should().NotBeNull();
        result.Timer!.RemainingSeconds.Should().Be(0);
        result.Timer.ActiveQuestion.Should().BeNull();
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenConnectionIdSupplied_RegistersConnectionLeaseUnderParticipant()
    {
        // Finding 5: the hub reconnect passes Context.ConnectionId; the aggregate registers it as a
        // presence lease inside the same serialized write, so the matching disconnect can be
        // decrement-guarded on that ConnectionId.
        var session = CreateScheduledSession();
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var admission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new JoinPolicy());
        session.DisconnectParticipant(
            admission.Participant.SessionParticipantId,
            new DateTimeOffset(2026, 6, 3, 10, 6, 0, TimeSpan.Zero));

        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var reconnectAt = new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero);
        var command = new ReconnectAuthenticatedParticipantCommand(
            session.LiveSessionId, team.TeamId, "Nora", "join-token", "conn-1");
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(reconnectAt));

        var result = await handler.Handle(command, CancellationToken.None);

        var participant = session.Participants.Single(
            candidate => candidate.SessionParticipantId == result.SessionParticipantId);
        participant.ActiveConnectionCount.Should().Be(1);
        participant.Connections.Single().ConnectionId.Should().Be("conn-1");
        participant.IsDisconnected.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenParticipantReconnectsToActiveQuestion_ReturnsAuthoritativeQuestionTimer()
    {
        // HU-22 / US16: a reconnecting participant gets the trustworthy active-substage
        // (trivia-question) remaining time immediately.
        var session = CreateScheduledTriviaSession();
        var identityReferenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(identityReferenceTeamId, "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var joinedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var activeAt = joinedAt.AddMinutes(2);
        var reconnectAt = activeAt.AddSeconds(10);
        // The mobile/Identity lobby hands the participant the Identity reference id, not the
        // runtime TeamId. Drive the whole reconnect with that id to exercise the real contract.
        var firstAdmission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            identityReferenceTeamId,
            joinedAt,
            new JoinPolicy());
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, joinedAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, activeAt.AddSeconds(-1), transitionPolicy);
        session.ActivateQuestion(0, activeAt);
        session.DisconnectParticipant(firstAdmission.Participant.SessionParticipantId, activeAt.AddSeconds(5));

        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, identityReferenceTeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var command = new ReconnectAuthenticatedParticipantCommand(
            session.LiveSessionId,
            identityReferenceTeamId,
            "Nora",
            null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(reconnectAt));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsReconnect.Should().BeTrue();
        result.TeamId.Should().Be(team.TeamId);
        result.Timer.Should().NotBeNull();
        result.Timer!.SessionState.Should().Be(nameof(SessionState.Active));
        result.Timer.RemainingSeconds.Should().Be(20);
        result.Timer.TimerStatus.Should().Be("Advancing");
        result.Timer.IsAdvancing.Should().BeTrue();
        result.Timer.AdvancingSince.Should().Be(activeAt);
        result.Timer.ActiveQuestion.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenParticipantReconnectsAfterSessionAdvanced_ResultCarriesCurrentStateAndTimerForHydration()
    {
        // HU-08 / AC3 (multi-device hydration lock): a device that first joined while the session was
        // Scheduled and later reconnects (2nd device / recovery) must be hydrated with the CURRENT
        // SessionState + authoritative timer at reconnect time — not the state captured at first join.
        // Distinct from the other reconnect tests: the state ADVANCED (Scheduled -> Active) between
        // join and reconnect, and there is no active trivia question (treasure-hunt), so the result
        // must still carry the current state + a session-level timer snapshot with no active-question countdown.
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var joinedAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);
        var firstAdmission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            team.TeamId,
            joinedAt,
            new JoinPolicy());
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, joinedAt.AddMinutes(1), transitionPolicy);
        session.MoveTo(SessionState.Active, joinedAt.AddMinutes(2), transitionPolicy);
        session.DisconnectParticipant(
            firstAdmission.Participant.SessionParticipantId,
            joinedAt.AddMinutes(3));

        var reconnectAt = joinedAt.AddMinutes(4);
        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", "join-token");
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(reconnectAt));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsReconnect.Should().BeTrue();
        // Current state at reconnect, not the Scheduled state captured at first join.
        result.SessionState.Should().Be(SessionState.Active.ToString());
        result.LastSeenAt.Should().Be(reconnectAt);
        result.Timer.Should().NotBeNull();
        result.Timer!.SessionState.Should().Be(nameof(SessionState.Active));
        result.Timer.TeamId.Should().Be(team.TeamId);
        result.Timer.ObservedAt.Should().Be(reconnectAt);
        // Treasure-hunt session: no active trivia question -> no active-question countdown in the hydration payload.
        result.Timer.ActiveQuestion.Should().BeNull();
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLateJoinTargetsActiveSession_ThrowsException()
    {
        var session = CreateActiveSession();
        var team = session.Teams.Single();
        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nova", null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<LateJoinNotAllowedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIdentityDeniesMembershipAccess_ThrowsForbiddenException()
    {
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: false);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var guard = CreateGuard(liveSessionId, teamId, isAllowed: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(liveSessionId, teamId, "Nora", null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIdIsNotAGuid_ThrowsUnauthorizedException()
    {
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("not-a-guid");
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDisconnectedParticipantTargetsFinishedSession_ThrowsException()
    {
        var transitionPolicy = new SessionStateTransitionPolicy();
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var participantIdentity = Guid.NewGuid();
        var admission = session.AdmitParticipant(
            participantIdentity,
            "Nora",
            team.TeamId,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new JoinPolicy());

        session.DisconnectParticipant(
            admission.Participant.SessionParticipantId,
            new DateTimeOffset(2026, 6, 3, 10, 6, 0, TimeSpan.Zero));
        session.MoveTo(SessionState.Preparing, new DateTimeOffset(2026, 6, 3, 10, 7, 0, TimeSpan.Zero), transitionPolicy);
        session.MoveTo(SessionState.Active, new DateTimeOffset(2026, 6, 3, 10, 8, 0, TimeSpan.Zero), transitionPolicy);
        // Finished is reached only via SessionCompletion (never a manual MoveTo); the single
        // treasure-hunt substage above completing with no next substage drives it there.
        session.CompleteActiveSubstageAndAdvance(new DateTimeOffset(2026, 6, 3, 10, 9, 0, TimeSpan.Zero), transitionPolicy);

        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(participantIdentity);
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "Nora", null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<LateJoinNotAllowedException>();
    }

    [Fact]
    public async Task Handle_WhenTeamIsAtCapacity_ThrowsException()
    {
        var session = CreateScheduledSession();
        var team = session.RegisterTeam("Alpha", "A-01", 2);
        session.AdmitParticipant(Guid.NewGuid(), "P1", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(-2), new JoinPolicy());
        session.AdmitParticipant(Guid.NewGuid(), "P2", team.TeamId, DateTimeOffset.UtcNow.AddMinutes(-1), new JoinPolicy());

        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(session.LiveSessionId, team.TeamId, "P3", null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<TeamCapacityReachedException>();
    }

    // Reconnect's first-join branch self-assigns the caller into the requested team, so it must clear the
    // same authorized set SelectTeam enforces. Otherwise reconnect is a way to obtain a membership that
    // Open Team Selection would refuse.
    [Fact]
    public async Task Handle_WhenFirstJoinTargetsTeamOutsideAuthorizedSet_ThrowsAndPersistsNothing()
    {
        var session = CreateScheduledSession();
        var alphaReferenceTeamId = Guid.NewGuid();
        var bravoReferenceTeamId = Guid.NewGuid();
        session.AssociateTeam(alphaReferenceTeamId, "Alpha", "A-01", 4);
        var bravo = session.AssociateTeam(bravoReferenceTeamId, "Bravo", "B-01", 4);

        var repository = CreateRepository(session);
        // Whitelisted for Alpha only; Bravo is attached, open, and has room — the authorized set is the
        // only thing standing between this caller and a Bravo membership.
        var guard = CreateGuard(
            session.LiveSessionId,
            bravo.TeamId,
            isAllowed: true,
            eligibleTeams: [new EligibleTeamDto(alphaReferenceTeamId, "Alpha", "A-01")]);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(
            session.LiveSessionId,
            bravoReferenceTeamId,
            "Mallory",
            null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<TeamNotInAuthorizedSetException>();
        session.Participants.Should().BeEmpty();
        repository.Verify(
            repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFirstJoinTargetsWhitelistedTeam_Admits()
    {
        var session = CreateScheduledSession();
        var alphaReferenceTeamId = Guid.NewGuid();
        var alpha = session.AssociateTeam(alphaReferenceTeamId, "Alpha", "A-01", 4);

        var repository = CreateRepository(session);
        var guard = CreateGuard(
            session.LiveSessionId,
            alpha.TeamId,
            isAllowed: true,
            eligibleTeams: [new EligibleTeamDto(alphaReferenceTeamId, "Alpha", "A-01")]);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(
            session.LiveSessionId,
            alphaReferenceTeamId,
            "Nora",
            null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsReconnect.Should().BeFalse();
        result.TeamId.Should().Be(alpha.TeamId);
    }

    // The Open Team Selection case the runtime-guard fix exists to unblock: an eligible participant with no
    // RegisteredTeamMembership has an empty whitelist, which means "every attached team", not "none".
    [Fact]
    public async Task Handle_WhenUnassignedParticipantFirstJoins_AdmitsUnderOpenTeamSelection()
    {
        var session = CreateScheduledSession();
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 4);

        var repository = CreateRepository(session);
        var guard = CreateGuard(session.LiveSessionId, team.TeamId, isAllowed: true, eligibleTeams: []);
        var currentUser = CreateCurrentUser(Guid.NewGuid());
        var command = new ReconnectAuthenticatedParticipantCommand(
            session.LiveSessionId,
            referenceTeamId,
            "Nora",
            null);
        var handler = CreateHandler(repository, guard, currentUser, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsReconnect.Should().BeFalse();
        result.TeamId.Should().Be(team.TeamId);
    }

    private static ReconnectAuthenticatedParticipantCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IRuntimeParticipationGuard> guard,
        Mock<ICurrentUser> currentUser,
        TimeProvider timeProvider)
    {
        return new ReconnectAuthenticatedParticipantCommandHandler(
            repository.Object,
            guard.Object,
            currentUser.Object,
            new JoinPolicy(),
            new OpenTeamSelectionPolicy(),
            timeProvider);
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return repository;
    }

    // eligibleTeams is the reference-team whitelist the guard hands back; empty (the default) means an
    // unassigned participant, for whom every attached team is selectable.
    private static Mock<IRuntimeParticipationGuard> CreateGuard(
        Guid liveSessionId,
        Guid teamId,
        bool isAllowed,
        IReadOnlyList<EligibleTeamDto>? eligibleTeams = null)
    {
        var guard = new Mock<IRuntimeParticipationGuard>();
        var setup = guard.Setup(g => g.EnsureAllowedAsync(
            liveSessionId,
            It.IsAny<CancellationToken>()));

        if (isAllowed)
        {
            setup.ReturnsAsync(new ParticipantEligibleTeamsDto(true, "eligible", eligibleTeams ?? []));
        }
        else
        {
            setup.ThrowsAsync(new ForbiddenAccessException());
        }

        return guard;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(Guid participantIdentity)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(participantIdentity.ToString());
        currentUser.SetupGet(user => user.Role).Returns("Participant");
        return currentUser;
    }

    private static LiveSession CreateScheduledSession()
    {
        return LiveSessionTestFactory.CreateScheduledTreasureHunt(
            "abc123",
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
    }

    private static LiveSession CreateScheduledTriviaSession()
    {
        return LiveSessionTestFactory.CreateScheduledTrivia(
            scheduledAt: new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
    }

    private static LiveSession CreateActiveSession()
    {
        var session = CreateScheduledSession();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero), transitionPolicy);
        session.MoveTo(SessionState.Active, new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero), transitionPolicy);
        return session;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
