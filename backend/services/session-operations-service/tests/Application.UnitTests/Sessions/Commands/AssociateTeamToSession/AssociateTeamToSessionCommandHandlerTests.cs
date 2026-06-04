using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Application.Sessions.Handlers;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.AssociateTeamToSession;

public sealed class AssociateTeamToSessionCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToFacade()
    {
        var command = new AssociateTeamToSessionCommand(Guid.NewGuid(), Guid.NewGuid());
        var expected = new AssociateTeamToSessionResultDto(
            command.LiveSessionId,
            Guid.NewGuid(),
            command.ReferenceTeamId,
            "Alpha",
            "A-01",
            "Scheduled",
            1);
        var facade = new Mock<ISessionTeamAssociationFacade>();
        facade
            .Setup(service => service.AssociateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new AssociateTeamToSessionCommandHandler(facade.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(expected);
        facade.Verify(service => service.AssociateAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
