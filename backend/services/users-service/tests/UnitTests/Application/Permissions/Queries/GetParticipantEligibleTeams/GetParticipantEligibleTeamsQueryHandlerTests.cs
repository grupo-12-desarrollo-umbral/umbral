using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Permissions;
using umbral_backend.Application.Permissions.Common;
using umbral_backend.Application.Permissions.Queries.GetParticipantEligibleTeams;
using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Application.Permissions.Queries.GetParticipantEligibleTeams;

public sealed class GetParticipantEligibleTeamsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsWhitelistedActiveTeamsForParticipant()
    {
        var actor = CreateUser(15, "kc-participant", Role.Participant);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        var handler = CreateHandler(actor, new[] { team });

        var result = await handler.Handle(new GetParticipantEligibleTeamsQuery(), CancellationToken.None);

        result.IsEligible.Should().BeTrue();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.Eligible);
        result.Teams.Should().ContainSingle()
            .Which.Should().Be(new EligibleTeamDto(team.TeamId, "Red Team", "RED-01"));
    }

    [Fact]
    public async Task Handle_ReturnsEligibleWithEmptySetWhenParticipantWhitelistedForNothing()
    {
        var actor = CreateUser(16, "kc-participant-empty", Role.Participant);
        var handler = CreateHandler(actor, Array.Empty<RegisteredTeam>());

        var result = await handler.Handle(new GetParticipantEligibleTeamsQuery(), CancellationToken.None);

        result.IsEligible.Should().BeTrue();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.Eligible);
        result.Teams.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DeniesDeactivatedActorWithoutTouchingTeams()
    {
        var actor = CreateUser(17, "kc-participant-inactive", Role.Participant);
        actor.DeactivateAccess();
        var teamRepository = new Mock<ITeamRepository>();
        var handler = CreateHandler(actor, teamRepository);

        var result = await handler.Handle(new GetParticipantEligibleTeamsQuery(), CancellationToken.None);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.UserAccessDeactivated);
        result.Teams.Should().BeEmpty();
        teamRepository.Verify(
            r => r.ListActiveByParticipantAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DeniesNonParticipantActor()
    {
        var actor = CreateUser(18, "kc-operator", Role.Operator);
        var handler = CreateHandler(actor, new Mock<ITeamRepository>());

        var result = await handler.Handle(new GetParticipantEligibleTeamsQuery(), CancellationToken.None);

        result.IsEligible.Should().BeFalse();
        result.ReasonCode.Should().Be(ParticipantMembershipAccessReasonCodes.UserNotParticipant);
        result.Teams.Should().BeEmpty();
    }

    private static GetParticipantEligibleTeamsQueryHandler CreateHandler(User actor, RegisteredTeam[] teams)
    {
        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(r => r.ListActiveByParticipantAsync(actor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);

        return CreateHandler(actor, teamRepository);
    }

    private static GetParticipantEligibleTeamsQueryHandler CreateHandler(User actor, Mock<ITeamRepository> teamRepository)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        return new GetParticipantEligibleTeamsQueryHandler(currentActor.Object, teamRepository.Object);
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
