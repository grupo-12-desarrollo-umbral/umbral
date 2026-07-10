using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Facades;

public sealed class SessionTeamAssociationFacadeTests
{
    [Fact]
    public async Task AssociateAsync_WhenTeamReferenceIsValid_AssociatesTeamAndPersistsSession()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var teamReference = new TeamReferenceDto(
            Guid.NewGuid(),
            "Alpha",
            "A-01",
            true,
            3);
        var teamCatalogClient = CreateTeamCatalogClient(teamReference);
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var result = await facade.AssociateAsync(
            new AssociateTeamToSessionCommand(session.LiveSessionId, teamReference.TeamId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.RuntimeTeamId.Should().NotBe(teamReference.TeamId);
        result.ReferenceTeamId.Should().Be(teamReference.TeamId);
        result.DisplayName.Should().Be("Alpha");
        result.TeamCode.Should().Be("A-01");
        result.SessionState.Should().Be(SessionState.Scheduled.ToString());
        result.AssociatedTeamCount.Should().Be(1);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssociateAsync_WhenTeamReferenceIsAlreadyAssociated_ThrowsDomainException()
    {
        var referenceTeamId = Guid.NewGuid();
        var session = CreateScheduledSession();
        session.AssociateTeam(referenceTeamId, "Alpha", "A-01", 2);

        var repository = CreateRepository(session);
        var teamCatalogClient = CreateTeamCatalogClient(new TeamReferenceDto(referenceTeamId, "Alpha", "A-01", true, 2));
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.AssociateAsync(
            new AssociateTeamToSessionCommand(session.LiveSessionId, referenceTeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateTeamAssociationInSessionException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssociateAsync_WhenTeamReferenceIsInactive_ThrowsValidationException()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var referenceTeamId = Guid.NewGuid();
        var teamCatalogClient = CreateTeamCatalogClient(new TeamReferenceDto(referenceTeamId, "Alpha", "A-01", false, 2));
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.AssociateAsync(
            new AssociateTeamToSessionCommand(session.LiveSessionId, referenceTeamId),
            CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey(nameof(AssociateTeamToSessionCommand.ReferenceTeamId));
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssociateAsync_WhenTeamReferenceDoesNotExist_ThrowsNotFoundException()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var missingReferenceTeamId = Guid.NewGuid();
        var teamCatalogClient = CreateMissingTeamCatalogClient();
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.AssociateAsync(
            new AssociateTeamToSessionCommand(session.LiveSessionId, missingReferenceTeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingReferenceTeamId}*");
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssociateAsync_WhenSessionIsNotScheduled_ThrowsDomainException()
    {
        var session = CreatePreparingSession();
        var repository = CreateRepository(session);
        var referenceTeamId = Guid.NewGuid();
        var teamCatalogClient = CreateTeamCatalogClient(new TeamReferenceDto(referenceTeamId, "Alpha", "A-01", true, 2));
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.AssociateAsync(
            new AssociateTeamToSessionCommand(session.LiveSessionId, referenceTeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<TeamAssociationRequiresScheduledSessionException>();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAssociatedTeamsAsync_WhenSessionHasAssociatedTeams_ReturnsAssociatedRuntimeTeams()
    {
        var session = CreateScheduledSession();
        var firstReferenceTeamId = Guid.NewGuid();
        var secondReferenceTeamId = Guid.NewGuid();
        session.AssociateTeam(firstReferenceTeamId, "Alpha", "A-01", 2);
        session.AssociateTeam(secondReferenceTeamId, "Beta", "B-02", 4);
        session.RegisterTeam("Walk-ins", "W-03", 1);

        var repository = CreateRepository(session);
        var teamCatalogClient = CreateMissingTeamCatalogClient();
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var result = await facade.GetAssociatedTeamsAsync(
            new GetAssociatedTeamsForSessionQuery(session.LiveSessionId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.Teams.Should().HaveCount(2);
        result.Teams.Should().Contain(team => team.ReferenceTeamId == firstReferenceTeamId && team.DisplayName == "Alpha");
        result.Teams.Should().Contain(team => team.ReferenceTeamId == secondReferenceTeamId && team.TeamCode == "B-02");
        result.Teams.Should().OnlyContain(team => team.JoinStatus == TeamJoinStatus.Open.ToString());
    }

    [Fact]
    public async Task GetAssociatedTeamsAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var liveSessionId = Guid.NewGuid();
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var teamCatalogClient = CreateMissingTeamCatalogClient();
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.GetAssociatedTeamsAsync(
            new GetAssociatedTeamsForSessionQuery(liveSessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{liveSessionId}*");
    }

    [Fact]
    public async Task AssociateByCodeAsync_WhenTeamReferenceIsValid_AssociatesTeamAndPersistsSession()
    {
        var session = CreateScheduledSession();
        var repository = CreateRepository(session);
        var teamReference = new TeamReferenceDto(
            Guid.NewGuid(),
            "Alpha",
            "A-01",
            true,
            3);
        var teamCatalogClient = CreateTeamCatalogClient(teamReference);
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var result = await facade.AssociateByCodeAsync(
            new AssociateTeamToSessionByCodeCommand(session.SessionCode, teamReference.TeamId),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.ReferenceTeamId.Should().Be(teamReference.TeamId);
        result.DisplayName.Should().Be("Alpha");
        result.TeamCode.Should().Be("A-01");
        result.AssociatedTeamCount.Should().Be(1);
        repository.Verify(repo => repo.GetBySessionCodeAsync(session.SessionCode, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssociateByCodeAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetBySessionCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var referenceTeamId = Guid.NewGuid();
        var teamCatalogClient = CreateTeamCatalogClient(new TeamReferenceDto(referenceTeamId, "Alpha", "A-01", true, 2));
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.AssociateByCodeAsync(
            new AssociateTeamToSessionByCodeCommand("MISSING1", referenceTeamId),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*MISSING1*");
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAssociatedTeamsByCodeAsync_WhenSessionHasAssociatedTeams_ReturnsAssociatedRuntimeTeams()
    {
        var session = CreateScheduledSession();
        var firstReferenceTeamId = Guid.NewGuid();
        var secondReferenceTeamId = Guid.NewGuid();
        session.AssociateTeam(firstReferenceTeamId, "Alpha", "A-01", 2);
        session.AssociateTeam(secondReferenceTeamId, "Beta", "B-02", 4);
        session.RegisterTeam("Walk-ins", "W-03", 1);

        var repository = CreateRepository(session);
        var teamCatalogClient = CreateMissingTeamCatalogClient();
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var result = await facade.GetAssociatedTeamsByCodeAsync(
            new GetAssociatedTeamsForSessionByCodeQuery(session.SessionCode),
            CancellationToken.None);

        result.LiveSessionId.Should().Be(session.LiveSessionId);
        result.Teams.Should().HaveCount(2);
        result.Teams.Should().Contain(team => team.ReferenceTeamId == firstReferenceTeamId && team.DisplayName == "Alpha");
        result.Teams.Should().Contain(team => team.ReferenceTeamId == secondReferenceTeamId && team.TeamCode == "B-02");
        repository.Verify(repo => repo.GetBySessionCodeAsync(session.SessionCode, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAssociatedTeamsByCodeAsync_WhenSessionDoesNotExist_ThrowsNotFoundException()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetBySessionCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSession?)null);
        var teamCatalogClient = CreateMissingTeamCatalogClient();
        var facade = new SessionTeamAssociationFacade(repository.Object, teamCatalogClient.Object);

        var act = async () => await facade.GetAssociatedTeamsByCodeAsync(
            new GetAssociatedTeamsForSessionByCodeQuery("MISSING1"),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*MISSING1*");
    }

    private static Mock<ILiveSessionRepository> CreateRepository(LiveSession session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(session.LiveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.GetBySessionCodeAsync(session.SessionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository
            .Setup(repo => repo.UpdateAsync(session, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return repository;
    }

    private static Mock<ITeamReferenceCatalogClient> CreateTeamCatalogClient(TeamReferenceDto teamReference)
    {
        var teamCatalogClient = new Mock<ITeamReferenceCatalogClient>();
        teamCatalogClient
            .Setup(client => client.GetByIdAsync(teamReference.TeamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamReference);

        return teamCatalogClient;
    }

    private static Mock<ITeamReferenceCatalogClient> CreateMissingTeamCatalogClient()
    {
        var teamCatalogClient = new Mock<ITeamReferenceCatalogClient>();
        teamCatalogClient
            .Setup(client => client.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamReferenceDto?)null);

        return teamCatalogClient;
    }

    private static LiveSession CreateScheduledSession()
    {
        return LiveSessionTestFactory.CreateScheduledTreasureHunt(
            "abc123",
            "Museum Hunt",
            45,
            new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero));
    }

    private static LiveSession CreatePreparingSession()
    {
        var session = CreateScheduledSession();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 2);
        session.MoveTo(
            SessionState.Preparing,
            new DateTimeOffset(2026, 6, 3, 10, 5, 0, TimeSpan.Zero),
            new SessionStateTransitionPolicy());
        return session;
    }
}
