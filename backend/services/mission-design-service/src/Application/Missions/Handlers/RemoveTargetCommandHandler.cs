using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.RemoveTarget;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class RemoveTargetCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<RemoveTargetCommand, MissionDto>
{
    public RemoveTargetCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(RemoveTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);
        MissionStructureEditor.FindTarget(mission, request.StageId, request.SubstageId, request.TargetId);

        mission.RemoveTarget(request.StageId, request.SubstageId, request.TargetId);

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
