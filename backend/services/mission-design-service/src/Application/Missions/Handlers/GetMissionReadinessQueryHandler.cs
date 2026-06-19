using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Application.Missions.Queries.GetMissionReadiness;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class GetMissionReadinessQueryHandler : IRequestHandler<GetMissionReadinessQuery, MissionReadinessDto>
{
    private readonly IMissionRepository _missionRepository;

    public GetMissionReadinessQueryHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionReadinessDto> Handle(GetMissionReadinessQuery request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Mission", request.Id);

        var failures = MissionActivationPolicy.EvaluateReadiness(mission);

        return new MissionReadinessDto(
            mission.Id,
            mission.ActivationState.ToString(),
            failures.Count == 0,
            failures);
    }
}
