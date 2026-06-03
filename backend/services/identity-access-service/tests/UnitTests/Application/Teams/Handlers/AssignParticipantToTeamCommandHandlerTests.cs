using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;
using umbral_backend.Application.Teams.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Handlers;

public sealed class AssignParticipantToTeamCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignsParticipantAndPersistsMembership()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = Team.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor.ExternalIdentityId, actor.Role.ToString());

        var membershipId = await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        membershipId.Should().NotBe(Guid.Empty);
        team.Memberships.Should().ContainSingle();
        team.DomainEvents.OfType<ParticipantAssignedToTeamEvent>().Should().ContainSingle();
        teamRepository.Verify(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllowsOperatorToAssignParticipant()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = Team.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor.ExternalIdentityId, actor.Role.ToString());

        var membershipId = await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

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
            .ReturnsAsync((Team?)null);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(Guid.NewGuid(), participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserDoesNotExist()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = Team.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, 99), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserIsOperator()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = Team.Register("Red Team", "RED-01");
        var operatorUser = CreateUser(11, "kc-operator", Role.Operator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, operatorUser), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, operatorUser.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotParticipantRoleException>();
    }

    [Fact]
    public async Task Handle_ThrowsWhenUserIsAdministrator()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = Team.Register("Red Team", "RED-01");
        var administratorUser = CreateUser(12, "kc-other-admin", Role.Administrator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, administratorUser), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, administratorUser.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotParticipantRoleException>();
    }

    [Fact]
    public async Task Handle_PropagatesInactiveTeamFailure()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = Team.Register("Red Team", "RED-01");
        team.Deactivate();
        team.ClearDomainEvents();

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<TeamNotActiveException>();
        teamRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PropagatesDuplicateAssignmentFailure()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var team = Team.Register("Red Team", "RED-01");
        team.AssignParticipant(participant.Id);
        team.ClearDomainEvents();

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdWithMembershipsAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor, participant), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(team.TeamId, participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ParticipantAlreadyAssignedToTeamException>();
        teamRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsParticipantCaller()
    {
        var actor = CreateUser(1, "kc-participant-actor", Role.Participant);
        var participant = CreateUser(10, "kc-participant", Role.Participant);
        var handler = CreateHandler(new Mock<ITeamRepository>(), CreateUserRepository(actor, participant), actor.ExternalIdentityId, actor.Role.ToString());

        var act = async () => await handler.Handle(new AssignParticipantToTeamCommand(Guid.NewGuid(), participant.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private static AssignParticipantToTeamCommandHandler CreateHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<IUserRepository> userRepository,
        string? currentUserId,
        string? currentUserRole)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(currentUserId);
        currentUser.SetupGet(user => user.Role).Returns(currentUserRole);

        return new AssignParticipantToTeamCommandHandler(
            teamRepository.Object,
            userRepository.Object,
            currentUser.Object,
            new AccessPolicy());
    }

    private static Mock<IUserRepository> CreateUserRepository(params User[] users)
    {
        var repository = new Mock<IUserRepository>();

        foreach (var user in users)
        {
            repository
                .Setup(repo => repo.GetByExternalIdentityIdAsync(user.ExternalIdentityId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(user);

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
