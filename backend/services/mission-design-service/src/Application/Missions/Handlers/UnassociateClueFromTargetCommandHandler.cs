using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UnassociateClueFromTarget;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class UnassociateClueFromTargetCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<UnassociateClueFromTargetCommand, MissionDto>
{
    public UnassociateClueFromTargetCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(UnassociateClueFromTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);

        MissionStructureEditor.UnassociateClueFromTarget(
            mission,
            request.StageId,
            request.SubstageId,
            request.TargetId);

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
