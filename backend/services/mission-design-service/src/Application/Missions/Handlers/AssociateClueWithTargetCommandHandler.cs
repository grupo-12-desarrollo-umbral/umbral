using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AssociateClueWithTarget;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class AssociateClueWithTargetCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<AssociateClueWithTargetCommand, MissionDto>
{
    public AssociateClueWithTargetCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(AssociateClueWithTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);
        MissionStructureEditor.FindTarget(mission, request.StageId, request.SubstageId, request.TargetId);
        var clue = MissionStructureEditor.FindClue(mission, request.StageId, request.SubstageId, request.ClueId);

        mission.AssociateClueWithTarget(request.StageId, request.SubstageId, request.TargetId, clue);

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
