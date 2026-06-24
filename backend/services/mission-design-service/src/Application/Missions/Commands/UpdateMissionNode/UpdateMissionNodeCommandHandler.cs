using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.UpdateMissionNode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Commands.UpdateMissionNode;

public sealed class UpdateMissionNodeCommandHandler
    : IRequestHandler<UpdateMissionNodeCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public UpdateMissionNodeCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(UpdateMissionNodeCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        MissionStructureEditor.RenameNode(
            mission,
            request.NodeId,
            request.Title,
            request.SequenceOrder,
            request.ClueText,
            request.ClueVisibilityPolicy is null
                ? null
                : Enum.Parse<ClueVisibilityPolicy>(request.ClueVisibilityPolicy));

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
