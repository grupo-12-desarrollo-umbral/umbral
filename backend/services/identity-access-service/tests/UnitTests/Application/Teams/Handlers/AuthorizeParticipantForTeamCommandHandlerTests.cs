using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.AuthorizeParticipantForTeam;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Handlers;

public sealed class AuthorizeParticipantForTeamCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignsParticipantAndPersistsMembership()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = RegisteredTeam.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor);

        var membershipId = await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        membershipId.Should().NotBe(Guid.Empty);
        team.Memberships.Should().ContainSingle();
        teamRepository.Verify(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllowsOperatorToAssignParticipant()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = RegisteredTeam.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor);

        var membershipId = await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        membershipId.Should().NotBe(Guid.Empty);
        team.Memberships.Should().ContainSingle();
        teamRepository.Verify(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsWhenTeamDoesNotExist()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisteredTeam?)null);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(Guid.NewGuid(), participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserDoesNotExist()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = RegisteredTeam.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, 99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserIsOperator()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        var operatorUser = CreateUser(11, "kc-operator", Role.Operator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, operatorUser), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, operatorUser.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotParticipantRoleException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserIsAdministrator()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        var administratorUser = CreateUser(12, "kc-other-admin", Role.Administrator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, administratorUser), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, administratorUser.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotParticipantRoleException>();
    }

    [Fact]
    public async Task Handle_PropagatesInactiveTeamFailure()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        team.Deactivate();
        team.ClearDomainEvents();

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TeamNotActiveException>();
        teamRepository.Verify(repo => repo.UpdateAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PropagatesDuplicateAssignmentFailure()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        team.AuthorizeParticipant(participant.Id);
        team.ClearDomainEvents();

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ParticipantAlreadyAuthorizedForTeamException>();
        teamRepository.Verify(repo => repo.UpdateAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsParticipantCaller()
    {
        var actor = CreateUser(1, "kc-participant-actor", Role.Participant);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var handler = CreateHandler(new Mock<ITeamRepository>(), CreateUserRepository(actor, participant), actor);

        var act = async () => await handler.Handle(new AuthorizeParticipantForTeamCommand(Guid.NewGuid(), participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
    }

    private static AuthorizeParticipantForTeamCommandHandler CreateHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<IUserRepository> userRepository,
        User actor)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor.Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        return new AuthorizeParticipantForTeamCommandHandler(
            teamRepository.Object,
            userRepository.Object,
            currentActor.Object,
            new AccessPolicy());
    }

    private static Mock<IUserRepository> CreateUserRepository(params User[] users)
    {
        var repository = new Mock<IUserRepository>();

        foreach (var user in users)
        {
            repository
                .Setup(repo => repo.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);
        }

        return repository;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
