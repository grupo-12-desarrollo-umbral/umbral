using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Commands.AddMissionNode;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Commands.DeactivateMission;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Commands.SetTriviaQuizSelection;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Commands.UpdateTriviaQuizSelection;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;
using umbral_backend.Application.UnitTests.Application.Missions.TestDoubles;

namespace umbral_backend.Application.UnitTests.Application.Missions.Handlers;

public sealed class GetMissionDetailQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenMissionExists_ReturnsMissionDetail()
    {
        var mission = new MissionDto(7, "Mission", "Briefing", "Advanced", 45, "Draft");
        var repository = new InMemoryMissionReadModelRepository(
            details: new Dictionary<int, MissionDto> { [mission.Id] = mission });
        var handler = new GetMissionDetailQueryHandler(repository);

        var result = await handler.Handle(new GetMissionDetailQuery(mission.Id), CancellationToken.None);

        result.Should().Be(mission);
    }

    [Fact]
    public async Task Handle_WhenMissionDoesNotExist_ThrowsNotFound()
    {
        var handler = new GetMissionDetailQueryHandler(new InMemoryMissionReadModelRepository());

        var act = () => handler.Handle(new GetMissionDetailQuery(42), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("Entity \"Mission\" (42) was not found.");
    }
}
