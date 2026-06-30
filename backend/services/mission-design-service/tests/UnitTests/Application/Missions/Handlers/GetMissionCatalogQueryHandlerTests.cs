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

public sealed class GetMissionCatalogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMissionCatalog()
    {
        var catalog = new[]
        {
            new MissionSummaryDto(1, "Mission One", "Briefing One", "Advanced", "Draft"),
            new MissionSummaryDto(2, "Mission Two", "Briefing Two", "Beginner", "Inactive")
        };
        var repository = new InMemoryMissionReadModelRepository(catalog);
        var handler = new GetMissionCatalogQueryHandler(repository);

        var result = await handler.Handle(new GetMissionCatalogQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(catalog);
    }
}
