using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.UpdateTarget;

public sealed class UpdateTargetCommandHandler
    : IRequestHandler<UpdateTargetCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public UpdateTargetCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(UpdateTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);
        MissionStructureEditor.FindTarget(mission, request.StageId, request.SubstageId, request.TargetId);

        mission.UpdateTarget(
            request.StageId,
            request.SubstageId,
            request.TargetId,
            request.Name,
            request.QrCode,
            request.SequenceOrder,
            request.IsActive,
            request.Score);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
