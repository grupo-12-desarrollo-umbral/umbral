using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Handlers;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public class CreateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesMissionAndReturnsDraftDto()
    {
        var repository = new InMemoryMissionRepository();
        var handler = new CreateMissionCommandHandler(repository);
        var command = new CreateMissionCommand("Test Mission", "Test Description", "Advanced", 45);

        var result = await handler.Handle(command, CancellationToken.None);

        repository.LastAddedMission.Should().NotBeNull();
        repository.LastAddedMission!.Id.Should().Be(result.Id);
        result.Status.Should().Be("Draft");
        result.Name.Should().Be("Test Mission");
        result.Description.Should().Be("Test Description");
        result.Difficulty.Should().Be("Advanced");
        result.MaximumTimeMinutes.Should().Be(45);
    }
}
