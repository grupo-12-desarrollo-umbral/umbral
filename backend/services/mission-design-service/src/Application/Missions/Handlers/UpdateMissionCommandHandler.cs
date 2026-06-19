using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateMission;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class UpdateMissionCommandHandler : IRequestHandler<UpdateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public UpdateMissionCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Mission", request.Id);

        mission.UpdateDetails(
            request.Name,
            request.Description,
            request.Difficulty,
            request.MaximumTimeMinutes);

        await _missionRepository.UpdateAsync(mission, cancellationToken);

        return MissionDtoMapper.Map(mission);
    }
}
