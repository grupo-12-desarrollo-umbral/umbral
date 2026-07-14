using umbral_backend.Application.Sessions.Commands.ReleaseClue;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.ReleaseClue;

public sealed class ReleaseClueCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesTheCompleteUseCaseToTheFacade()
    {
        var command = new ReleaseClueCommand(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid());
        var expected = new ReleaseClueResultDto(command.TargetId, command.ClueId, [command.TeamId!.Value]);
        var facade = new Mock<IClueReleaseFacade>();
        facade.Setup(x => x.ReleaseCluesAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var handler = new ReleaseClueCommandHandler(facade.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeSameAs(expected);
        facade.Verify(x => x.ReleaseCluesAsync(command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
