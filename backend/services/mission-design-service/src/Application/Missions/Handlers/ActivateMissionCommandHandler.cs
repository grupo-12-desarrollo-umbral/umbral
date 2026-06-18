using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.ActivateMission;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class ActivateMissionCommandHandler : IRequestHandler<ActivateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public ActivateMissionCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(ActivateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Mission", request.Id);

        mission.Activate();

        await _missionRepository.UpdateAsync(mission, cancellationToken);

        return MissionDtoMapper.Map(mission);
    }
}
