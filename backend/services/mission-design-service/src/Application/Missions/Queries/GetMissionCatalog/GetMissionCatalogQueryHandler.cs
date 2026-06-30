using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;

namespace umbral_backend.Application.Missions.Queries.GetMissionCatalog;

public sealed class GetMissionCatalogQueryHandler : IRequestHandler<GetMissionCatalogQuery, IReadOnlyList<MissionSummaryDto>>
{
    private readonly IMissionReadModelRepository _missionReadModelRepository;

    public GetMissionCatalogQueryHandler(IMissionReadModelRepository missionReadModelRepository)
    {
        _missionReadModelRepository = missionReadModelRepository;
    }

    public async Task<IReadOnlyList<MissionSummaryDto>> Handle(GetMissionCatalogQuery request, CancellationToken cancellationToken)
    {
        return await _missionReadModelRepository.GetMissionCatalogAsync(cancellationToken);
    }
}
