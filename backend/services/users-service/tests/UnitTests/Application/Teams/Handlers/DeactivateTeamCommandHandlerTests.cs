using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.DeactivateTeam;
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
        var team = RegisteredTeam.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateCurrentActor(actor));

        await handler.Handle(new DeactivateTeamCommand(team.TeamId), CancellationToken.None);

        team.IsActive.Should().BeFalse();
        team.DomainEvents.OfType<TeamDeactivatedEvent>().Should().ContainSingle();
        teamRepository.Verify(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsWhenTeamIsAlreadyInactive()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        var team = RegisteredTeam.Register("Red Team", "RED-01");
        team.Deactivate();

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateHandler(teamRepository, CreateCurrentActor(actor));

        var act = async () => await handler.Handle(new DeactivateTeamCommand(team.TeamId), CancellationToken.None);

        await act.Should().ThrowAsync<TeamAlreadyDeactivatedException>();
        teamRepository.Verify(repo => repo.UpdateAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsWhenTeamDoesNotExist()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegisteredTeam?)null);

        var handler = CreateHandler(teamRepository, CreateCurrentActor(actor));

        var act = async () => await handler.Handle(new DeactivateTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_OperatorCanDeactivateTeam()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        var team = RegisteredTeam.Register("Red Team", "RED-01");

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.GetByIdAsync(team.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        teamRepository
            .Setup(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(teamRepository, CreateCurrentActor(actor));

        await handler.Handle(new DeactivateTeamCommand(team.TeamId), CancellationToken.None);

        team.IsActive.Should().BeFalse();
        team.DomainEvents.OfType<TeamDeactivatedEvent>().Should().ContainSingle();
        teamRepository.Verify(repo => repo.UpdateAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsMissingCurrentUserIdentity()
    {
        var handler = CreateHandler(new Mock<ITeamRepository>(), UnauthorizedActor());

        var act = async () => await handler.Handle(new DeactivateTeamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static DeactivateTeamCommandHandler CreateHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<ICurrentActor> currentActor)
    {
        return new DeactivateTeamCommandHandler(
            teamRepository.Object,
            currentActor.Object,
            new AccessPolicy());
    }

    private static Mock<ICurrentActor> CreateCurrentActor(User actor)
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        return currentActor;
    }

    private static Mock<ICurrentActor> UnauthorizedActor()
    {
        var currentActor = new Mock<ICurrentActor>();
        currentActor
            .Setup(a => a.GetActorAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        return currentActor;
    }

    private static User CreateUser(int id, string externalIdentityId, Role role)
    {
        var user = User.Provision(externalIdentityId, $"{role} User", $"{externalIdentityId}@example.com", role);
        user.Id = id;
        return user;
    }
}
