using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class UpdateMissionNodeCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<UpdateMissionNodeCommand, MissionDto>
{
    public UpdateMissionNodeCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(UpdateMissionNodeCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);

        MissionStructureEditor.RenameNode(
            mission,
            request.NodeId,
            request.Title,
            request.SequenceOrder,
            request.ClueText,
            request.ClueVisibilityPolicy is null
                ? null
                : Enum.Parse<ClueVisibilityPolicy>(request.ClueVisibilityPolicy));

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
