using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

public sealed class ValidateParticipantMembershipAccessQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllowedDecisionForParticipantMembership()
    {
        var actor = CreateUser(15, "kc-participant", Role.Participant);
        var team = CreateTeamWithParticipant(actor.Id);
        var handler = CreateHandler(actor, team);
        var query = new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Capability.Should().Be(nameof(ProtectedCapability.ParticipantExperience));
        result.IsAllowed.Should().BeTrue();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.Eligible);
        result.LiveSessionId.Should().Be(query.LiveSessionId);
        result.TeamId.Should().Be(team.TeamId);
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionForNonParticipantActor()
    {
        var actor = CreateUser(16, "kc-operator", Role.Operator);
        var team = CreateTeamWithParticipant(actor.Id);
        var handler = CreateHandler(actor, team);

        var result = await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.UserNotParticipant);
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionWhenParticipantHasNoMembership()
    {
        var actor = CreateUser(17, "kc-participant-no-membership", Role.Participant);
        var team = RegisteredTeam.Register("Blue Team", "BLUE-01");
        var handler = CreateHandler(actor, team);

        var result = await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.ParticipantNotAuthorizedForRegisteredTeam);
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionWhenParticipantTargetsAnotherTeam()
    {
        var actor = CreateUser(18, "kc-participant-other-team", Role.Participant);
        var ownTeam = CreateTeamWithParticipant(actor.Id);
        var otherTeam = RegisteredTeam.Register("Green Team", "GREEN-01");
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(ownTeam.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownTeam);
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(otherTeam.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherTeam);

        var handler = CreateHandler(actor, teamRepository);

        var result = await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), otherTeam.TeamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.ParticipantNotAuthorizedForRegisteredTeam);
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionWhenActorIsDeactivated()
    {
        var actor = CreateUser(19, "kc-participant-inactive", Role.Participant);
        actor.DeactivateAccess();
        var team = CreateTeamWithParticipant(actor.Id);
        var handler = CreateHandler(actor, team);

        var result = await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), team.TeamId),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.UserAccessDeactivated);
    }

    [Fact]
    public async Task Handle_ReturnsDeniedDecisionWhenTeamDoesNotExist()
    {
        var actor = CreateUser(20, "kc-participant-missing-team", Role.Participant);
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisteredTeam?)null);

        var handler = CreateHandler(actor, teamRepository);

        var result = await handler.Handle(
            new ValidateParticipantMembershipAccessQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.RegisteredTeamNotFound);
    }

    private static ParticipantMembershipAccessAuthorizationProxy CreateHandler(User actor, RegisteredTeam team)
    {
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repository => repository.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        return CreateHandler(actor, teamRepository);
    }

    private static ParticipantMembershipAccessAuthorizationProxy CreateHandler(
        User actor,
        Mock<ITeamRepository> teamRepository)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        return new ParticipantMembershipAccessAuthorizationProxy(
            teamRepository.Object,
            currentActor.Object,
            new ValidateParticipantMembershipAccessQueryHandler());
    }

    private static RegisteredTeam CreateTeamWithParticipant(int participantId)
    {
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        team.AuthorizeParticipant(participantId);
        return team;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
