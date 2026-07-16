using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.SelectTeam;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.SelectTeam;

// Handler delegates to LiveSession.SelectTeam (#89); these assert wiring/gates, not domain behaviour.
public sealed class SelectTeamCommandHandlerTests
{
    private const string SessionCode = "abc123";

    [Fact]
    public async Task Handle_WhenEligibleParticipantPicksWhitelistedTeam_UpdatesAndReturnsMembership()
    {
        var session = CreateScheduledSession();
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 4);
        var identity = Guid.NewGuid();

        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: true, referenceTeamId);
        var currentUser = CreateCurrentUser(identity, "nora.smith@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var result = await handler.Handle(new SelectTeamCommand(SessionCode, team.TeamId), CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.TeamId.Should().Be(team.TeamId);
        result.State.Should().Be(SessionState.Scheduled.ToString());
        var membership = team.Members.Single(member => member.IsActive);
        result.SessionParticipantId.Should().Be(membership.SessionParticipantId);
        result.TeamMembershipId.Should().Be(membership.TeamMemberId);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUnassignedButEligibleParticipant_PicksAnyAttachedTeam()
    {
        // IsEligible=true with an empty whitelist is a valid unassigned → Open Team Selection participant.
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "guest@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var result = await handler.Handle(new SelectTeamCommand(SessionCode, team.TeamId), CancellationToken.None);

        result.TeamId.Should().Be(team.TeamId);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenParticipantNotEligible_ThrowsForbidden()
    {
        var session = CreateScheduledSession();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: false);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "denied@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var act = async () => await handler.Handle(new SelectTeamCommand(SessionCode, team.TeamId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ThrowsNotFound()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetBySessionCodeAsync(SessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var eligible = CreateEligibleClient(isEligible: true);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "nora@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var act = async () => await handler.Handle(new SelectTeamCommand(SessionCode, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCurrentUserIdIsNotAGuid_ThrowsUnauthorized()
    {
        var session = CreateScheduledSession();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: true);
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns("not-a-guid");
        var handler = CreateHandler(repository, eligible, currentUser);

        var act = async () => await handler.Handle(new SelectTeamCommand(SessionCode, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_WhenSelectionIsFrozen_PropagatesDomainException()
    {
        var session = CreateScheduledSession();
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 4);
        var transitionPolicy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, new DateTimeOffset(2026, 6, 3, 10, 1, 0, TimeSpan.Zero), transitionPolicy);
        session.MoveTo(SessionState.Active, new DateTimeOffset(2026, 6, 3, 10, 2, 0, TimeSpan.Zero), transitionPolicy);

        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: true, referenceTeamId);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "nora@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var act = async () => await handler.Handle(new SelectTeamCommand(SessionCode, team.TeamId), CancellationToken.None);

        await act.Should().ThrowAsync<OpenTeamSelectionClosedException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetTeamIsAtCapacity_PropagatesDomainException()
    {
        var session = CreateScheduledSession();
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 1);
        session.SelectTeam(Guid.NewGuid(), "First", team.TeamId, new HashSet<Guid> { referenceTeamId }, DateTimeOffset.UtcNow, new OpenTeamSelectionPolicy());

        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: true, referenceTeamId);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "second@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var act = async () => await handler.Handle(new SelectTeamCommand(SessionCode, team.TeamId), CancellationToken.None);

        await act.Should().ThrowAsync<TeamCapacityReachedException>();
    }

    [Fact]
    public async Task Handle_WhenTargetTeamNotInAuthorizedSet_PropagatesDomainException()
    {
        var session = CreateScheduledSession();
        var allowedReferenceTeamId = Guid.NewGuid();
        session.AssociateTeam(allowedReferenceTeamId, "Alpha", "A-01", 4);
        var forbiddenTeam = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);

        var repository = CreateRepository(session);
        // Whitelisted for Alpha only, so Alpha is the selectable set and Bravo is rejected.
        var eligible = CreateEligibleClient(isEligible: true, allowedReferenceTeamId);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "nora@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        var act = async () => await handler.Handle(new SelectTeamCommand(SessionCode, forbiddenTeam.TeamId), CancellationToken.None);

        await act.Should().ThrowAsync<TeamNotInAuthorizedSetException>();
    }

    [Fact]
    public async Task Handle_JoinsParticipantUnderTheCurrentUserDisplayName()
    {
        // The handler no longer derives a name; it forwards ICurrentUser.DisplayName verbatim.
        // The resolution ladder behind that value is covered by CurrentUserTests.
        var session = CreateScheduledSession();
        var referenceTeamId = Guid.NewGuid();
        var team = session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 4);
        var repository = CreateRepository(session);
        var eligible = CreateEligibleClient(isEligible: true, referenceTeamId);
        var currentUser = CreateCurrentUser(Guid.NewGuid(), "nora.smith@example.com");
        var handler = CreateHandler(repository, eligible, currentUser);

        await handler.Handle(new SelectTeamCommand(SessionCode, team.TeamId), CancellationToken.None);

        session.Participants.Single().DisplayName.Should().Be("Nora Smith");
    }

    private static SelectTeamCommandHandler CreateHandler(
        Mock<ILiveSessionRepository> repository,
        Mock<IParticipantEligibleTeamsClient> eligible,
        Mock<ICurrentUser> currentUser)
    {
        return new SelectTeamCommandHandler(
            repository.Object,
            eligible.Object,
            currentUser.Object,
            new OpenTeamSelectionPolicy(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero)));
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetBySessionCodeAsync(SessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static Mock<IParticipantEligibleTeamsClient> CreateEligibleClient(bool isEligible, params Guid[] referenceTeamIds)
    {
        var teams = referenceTeamIds.Select(id => new EligibleTeamDto(id, "Team", "T-01")).ToList();
        var eligible = new Mock<IParticipantEligibleTeamsClient>();
        eligible
            .Setup(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantEligibleTeamsDto(isEligible, isEligible ? "eligible" : "denied", teams));
        return eligible;
    }

    private static Mock<ICurrentUser> CreateCurrentUser(Guid identity, string email)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(identity.ToString());
        currentUser.SetupGet(user => user.Email).Returns(email);
        currentUser.SetupGet(user => user.Role).Returns("Participant");
        currentUser.SetupGet(user => user.DisplayName).Returns("Nora Smith");
        return currentUser;
    }

    private static LiveSession CreateScheduledSession()
    {
        return LiveSessionTestFactory.CreateScheduledTreasureHunt(
            SessionCode,
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
