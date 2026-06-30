using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.SetTriviaQuizSelection;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
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
