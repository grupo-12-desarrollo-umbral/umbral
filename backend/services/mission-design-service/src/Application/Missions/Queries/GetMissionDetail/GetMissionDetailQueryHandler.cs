using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.Queries.GetDifficultyCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionCatalog;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;
using umbral_backend.Application.Missions.Queries.GetMissionDetail;

namespace umbral_backend.Application.Missions.Queries.GetMissionDetail;

public sealed class GetMissionDetailQueryHandler : IRequestHandler<GetMissionDetailQuery, MissionDto>
{
    private readonly IMissionReadModelRepository _missionReadModelRepository;

    public GetMissionDetailQueryHandler(IMissionReadModelRepository missionReadModelRepository)
    {
        _missionReadModelRepository = missionReadModelRepository;
    }

    public async Task<MissionDto> Handle(GetMissionDetailQuery request, CancellationToken cancellationToken)
    {
        var mission = await _missionReadModelRepository.GetMissionDetailAsync(request.Id, cancellationToken);

        if (mission is null)
        {
            throw new NotFoundException("Mission", request.Id);
        }

        return mission;
    }
}
