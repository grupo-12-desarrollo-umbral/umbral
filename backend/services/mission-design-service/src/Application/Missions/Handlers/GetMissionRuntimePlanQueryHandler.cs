using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissionRuntimePlan;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class GetMissionRuntimePlanQueryHandler : IRequestHandler<GetMissionRuntimePlanQuery, MissionRuntimePlanDto>
{
    private readonly IMissionReadModelRepository _missionReadModelRepository;

    public GetMissionRuntimePlanQueryHandler(IMissionReadModelRepository missionReadModelRepository)
    {
        _missionReadModelRepository = missionReadModelRepository;
    }

    public async Task<MissionRuntimePlanDto> Handle(
        GetMissionRuntimePlanQuery request,
        CancellationToken cancellationToken)
    {
        var missionRuntimePlan = await _missionReadModelRepository.GetMissionRuntimePlanAsync(
            request.Id,
            cancellationToken);

        if (missionRuntimePlan is null)
        {
            throw new NotFoundException("Mission", request.Id);
        }

        return missionRuntimePlan;
    }
}
