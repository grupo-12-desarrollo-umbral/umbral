using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Application.Missions.DTOs;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Handlers;

public sealed class AssignSubstagePlayModeCommandHandler
    : MissionCommandHandlerBase, IRequestHandler<AssignSubstagePlayModeCommand, MissionDto>
{
    public AssignSubstagePlayModeCommandHandler(IMissionRepository missionRepository)
        : base(missionRepository)
    {
    }

    public async Task<MissionDto> Handle(AssignSubstagePlayModeCommand request, CancellationToken cancellationToken)
    {
        var mission = await GetMissionAsync(request.MissionId, cancellationToken);

        MissionStructureEditor.AssignPlayMode(
            mission,
            request.StageId,
            request.SubstageId,
            Enum.Parse<SubstagePlayMode>(request.PlayMode));

        await UpdateAndReturnAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
