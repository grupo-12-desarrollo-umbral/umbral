using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;

public sealed class AssociateClueWithTargetCommandHandler
    : IRequestHandler<AssociateClueWithTargetCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public AssociateClueWithTargetCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(AssociateClueWithTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);
        MissionStructureEditor.FindTarget(mission, request.StageId, request.SubstageId, request.TargetId);
        var clue = MissionStructureEditor.FindClue(mission, request.StageId, request.SubstageId, request.ClueId);

        mission.AssociateClueWithTarget(request.StageId, request.SubstageId, request.TargetId, clue);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
