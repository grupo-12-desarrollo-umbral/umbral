using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.DeactivateTeam;
using umbral_backend.Application.Teams.Handlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Handlers;

public sealed class DeactivateTeamCommandHandlerTests
{
    [Fact]
    public async Task Handle_DeactivatesTeamAndPersistsIt()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = Team.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor), actor.ExternalIdentityId);

        await handler.Handle(new DeactivateTeamCommand(team.TeamId), CancellationToken.None);

        team.IsActive.Should().BeFalse();
        team.DomainEvents.OfType<TeamDeactivatedEvent>().Should().ContainSingle();
        teamRepository.Verify(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsWhenTeamIsAlreadyInactive()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = Team.Register("Red Team", "RED-01");
        team.Deactivate();

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor), actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new DeactivateTeamCommand(team.TeamId), CancellationToken.None);

        await act.Should().ThrowAsync<TeamAlreadyDeactivatedException>();
        teamRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsWhenTeamDoesNotExist()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var handler = CreateHandler(teamRepository, CreateUserRepository(actor), actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new DeactivateTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_RejectsNonAdministratorCaller()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var handler = CreateHandler(new Mock<ITeamRepository>(), CreateUserRepository(actor), actor.ExternalIdentityId);

        var act = async () => await handler.Handle(new DeactivateTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_RejectsMissingCurrentUserIdentity()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var handler = CreateHandler(new Mock<ITeamRepository>(), CreateUserRepository(actor), null);

        var act = async () => await handler.Handle(new DeactivateTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static DeactivateTeamCommandHandler CreateHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<IUserRepository> userRepository,
        string? currentUserId)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Id).Returns(currentUserId);

        return new DeactivateTeamCommandHandler(
            teamRepository.Object,
            userRepository.Object,
            currentUser.Object,
            new AccessPolicy());
    }

    private static Mock<IUserRepository> CreateUserRepository(User actor)
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(repo => repo.GetByExternalIdentityIdAsync(actor.ExternalIdentityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        return repository;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
