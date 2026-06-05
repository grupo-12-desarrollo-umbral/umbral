using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSessionReference;
using umbral_backend.Application.Sessions.Handlers;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.UnitTests.Application.Sessions.Handlers;

public sealed class AssociateTeamToSessionReferenceCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenReferenceMissing_CreatesReferenceAndAssociatesTeam()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        LiveSessionReference? addedReference = null;

        var repository = new Mock<ILiveSessionReferenceRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveSessionReference?)null);
        repository
            .Setup(repo => repo.AddAsync(It.IsAny<LiveSessionReference>(), It.IsAny<CancellationToken>()))
            .Callback<LiveSessionReference, CancellationToken>((reference, _) => addedReference = reference)
            .Returns(Task.CompletedTask);

        var handler = new AssociateTeamToSessionReferenceCommandHandler(repository.Object);

        await handler.Handle(
            new AssociateTeamToSessionReferenceCommand(liveSessionId, "ABC123", teamId),
            CancellationToken.None);

        addedReference.Should().NotBeNull();
        addedReference!.LiveSessionId.Should().Be(liveSessionId);
        addedReference.SessionCode.Should().Be("ABC123");
        addedReference.TeamAssociations.Should().ContainSingle(association => association.TeamId == teamId);
        repository.Verify(repo => repo.AddAsync(It.IsAny<LiveSessionReference>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSessionReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenReferenceExists_AssociatesTeamAndPersists()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var reference = LiveSessionReference.Create(liveSessionId, "ABC123");

        var repository = new Mock<ILiveSessionReferenceRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        var handler = new AssociateTeamToSessionReferenceCommandHandler(repository.Object);

        await handler.Handle(
            new AssociateTeamToSessionReferenceCommand(liveSessionId, "ABC123", teamId),
            CancellationToken.None);

        reference.TeamAssociations.Should().ContainSingle(association => association.TeamId == teamId);
        repository.Verify(repo => repo.UpdateAsync(reference, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.AddAsync(It.IsAny<LiveSessionReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTeamAlreadyAssociated_IsIdempotentSuccess()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var reference = LiveSessionReference.Create(liveSessionId, "ABC123");
        reference.AssociateTeam(teamId);

        var repository = new Mock<ILiveSessionReferenceRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reference);

        var handler = new AssociateTeamToSessionReferenceCommandHandler(repository.Object);

        await handler.Handle(
            new AssociateTeamToSessionReferenceCommand(liveSessionId, "ABC123", teamId),
            CancellationToken.None);

        reference.TeamAssociations.Should().ContainSingle(association => association.TeamId == teamId);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LiveSessionReference>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.AddAsync(It.IsAny<LiveSessionReference>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
