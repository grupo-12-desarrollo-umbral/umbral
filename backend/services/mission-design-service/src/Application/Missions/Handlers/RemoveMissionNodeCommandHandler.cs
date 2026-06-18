using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class RemoveMissionNodeCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<RemoveMissionNodeCommand, MissionDto>
{
    public RemoveMissionNodeCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(RemoveMissionNodeCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);

        MissionStructureEditor.RemoveNode(mission, request.NodeId);

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
