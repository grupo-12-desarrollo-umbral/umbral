using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.RemoveTarget;

public sealed class RemoveTargetCommandHandler
    : IRequestHandler<RemoveTargetCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public RemoveTargetCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(RemoveTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);
        MissionStructureEditor.FindTarget(mission, request.StageId, request.SubstageId, request.TargetId);

        mission.RemoveTarget(request.StageId, request.SubstageId, request.TargetId);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
