using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class AddTargetCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<AddTargetCommand, MissionDto>
{
    public AddTargetCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(AddTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);
        MissionStructureEditor.FindSubstage(mission, request.StageId, request.SubstageId);

        mission.AddTarget(
            request.StageId,
            request.SubstageId,
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
