using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;

namespace umbral_backend.Application.Common.Interfaces;

public interface IMissionReadModelRepository
{
    Task<IReadOnlyList<MissionSummaryDto>> GetMissionCatalogAsync(CancellationToken cancellationToken);

    Task<MissionDto?> GetMissionDetailAsync(int missionId, CancellationToken cancellationToken);

    Task<MissionRuntimePlanDto?> GetMissionRuntimePlanAsync(int missionId, CancellationToken cancellationToken);
}
