using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface IMissionReadModelRepository
{
    Task<IReadOnlyList<MissionSummaryDto>> GetMissionCatalogAsync(CancellationToken cancellationToken);

    Task<MissionDto?> GetMissionDetailAsync(int missionId, CancellationToken cancellationToken);

    Task<MissionRuntimePlanDto?> GetMissionRuntimePlanAsync(int missionId, CancellationToken cancellationToken);
}
