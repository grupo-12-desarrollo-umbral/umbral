using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.RegisterTeam;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Application.Teams.Handlers;

public sealed class RegisterTeamCommandHandlerTests
{
    [Fact]
    public async Task Handle_RegistersTeamAndPersistsIt()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);
        RegisteredTeam? addedTeam = null;

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.TeamCodeExistsAsync("RED-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        teamRepository
            .Setup(repo => repo.AddAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()))
            .Callback<RegisteredTeam, CancellationToken>((team, _) => addedTeam = team)
            .Returns(Task.CompletedTask);

        var handler = CreateRegisterHandler(teamRepository, CreateCurrentActor(actor));

        var teamId = await handler.Handle(new RegisterTeamCommand("Red Team", "RED-01"), CancellationToken.None);

        teamId.Should().NotBe(Guid.Empty);
        addedTeam.Should().NotBeNull();
        addedTeam!.TeamId.Should().Be(teamId);
        addedTeam.DisplayName.Should().Be("Red Team");
        addedTeam.TeamCode.Should().Be("RED-01");
        addedTeam.DomainEvents.OfType<TeamRegisteredEvent>().Should().ContainSingle();
        teamRepository.Verify(repo => repo.AddAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AllowsOperatorToRegisterTeam()
    {
        var actor = CreateUser(1, "kc-operator", Role.Operator);
        RegisteredTeam? addedTeam = null;

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.TeamCodeExistsAsync("RED-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        teamRepository
            .Setup(repo => repo.AddAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()))
            .Callback<RegisteredTeam, CancellationToken>((team, _) => addedTeam = team)
            .Returns(Task.CompletedTask);

        var handler = CreateRegisterHandler(teamRepository, CreateCurrentActor(actor));

        var teamId = await handler.Handle(new RegisterTeamCommand("Red Team", "RED-01"), CancellationToken.None);

        teamId.Should().NotBe(Guid.Empty);
        addedTeam.Should().NotBeNull();
        addedTeam!.TeamId.Should().Be(teamId);
        teamRepository.Verify(repo => repo.AddAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RejectsDuplicateTeamCode()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.TeamCodeExistsAsync("RED-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateRegisterHandler(teamRepository, CreateCurrentActor(actor));

        var act = async () => await handler.Handle(new RegisterTeamCommand("Red Team", "RED-01"), CancellationToken.None);

        await act.Should().ThrowAsync<TeamCodeAlreadyExistsException>()
            .WithMessage("Team code 'RED-01' already exists.");
        teamRepository.Verify(repo => repo.AddAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsBlankDisplayName()
    {
        var actor = CreateUser(1, "kc-admin", Role.Administrator);

        var teamRepository = new Mock<ITeamRepository>();
        teamRepository
            .Setup(repo => repo.TeamCodeExistsAsync("RED-01", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateRegisterHandler(teamRepository, CreateCurrentActor(actor));

        var act = async () => await handler.Handle(new RegisterTeamCommand(" ", "RED-01"), CancellationToken.None);

        await act.Should().ThrowAsync<TeamDisplayNameRequiredException>();
        teamRepository.Verify(repo => repo.AddAsync(It.IsAny<RegisteredTeam>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsParticipantCaller()
    {
        var actor = CreateUser(1, "kc-participant", Role.Participant);
        var handler = CreateRegisterHandler(new Mock<ITeamRepository>(), CreateCurrentActor(actor));

        var act = async () => await handler.Handle(new RegisterTeamCommand("Red Team", "RED-01"), CancellationToken.None);

        await act.Should().ThrowAsync<UserRoleNotAuthorizedException>();
    }

    [Fact]
    public async Task Handle_RejectsMissingCurrentUserIdentity()
    {
        var handler = CreateRegisterHandler(new Mock<ITeamRepository>(), UnauthorizedActor());

        var act = async () => await handler.Handle(new RegisterTeamCommand("Red Team", "RED-01"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static RegisterTeamCommandHandler CreateRegisterHandler(
        Mock<ITeamRepository> teamRepository,
        Mock<ICurrentActor> currentActor)
    {
        return new RegisterTeamCommandHandler(
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
