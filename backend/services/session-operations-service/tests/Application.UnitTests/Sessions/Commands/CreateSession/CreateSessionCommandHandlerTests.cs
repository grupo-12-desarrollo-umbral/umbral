using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Handlers;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.CreateSession;

public sealed class CreateSessionCommandHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToFacade()
    {
        var facade = new Mock<ICreateSessionFacade>();
        var command = new CreateSessionCommand(
            7,
            "Mission Session",
            15,
            new DateTimeOffset(2026, 6, 4, 15, 0, 0, TimeSpan.Zero));
        var expected = new CreateSessionResultDto(
            Guid.NewGuid(),
            "ABC123",
            command.Title,
            "Scheduled",
            command.ScheduledAt);

        facade
            .Setup(instance => instance.CreateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new CreateSessionCommandHandler(facade.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().Be(expected);
    }
}
