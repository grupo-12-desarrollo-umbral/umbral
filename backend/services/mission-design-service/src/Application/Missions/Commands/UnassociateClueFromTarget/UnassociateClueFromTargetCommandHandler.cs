using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;

public sealed class UnassociateClueFromTargetCommandHandler
    : IRequestHandler<UnassociateClueFromTargetCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public UnassociateClueFromTargetCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(UnassociateClueFromTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        MissionStructureEditor.UnassociateClueFromTarget(
            mission,
            request.StageId,
            request.SubstageId,
            request.TargetId);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
