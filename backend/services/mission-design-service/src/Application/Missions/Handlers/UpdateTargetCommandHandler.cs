using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateTarget;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class UpdateTargetCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<UpdateTargetCommand, MissionDto>
{
    public UpdateTargetCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(UpdateTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);
        MissionStructureEditor.FindTarget(mission, request.StageId, request.SubstageId, request.TargetId);

        mission.UpdateTarget(
            request.StageId,
            request.SubstageId,
            request.TargetId,
            request.Name,
            request.QrCode,
            request.SequenceOrder,
            request.IsActive);

        if (request.WinnerScore is not null)
        {
            mission.SetTreasureHuntWinnerScore(request.StageId, request.SubstageId, request.WinnerScore.Value);
        }

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
