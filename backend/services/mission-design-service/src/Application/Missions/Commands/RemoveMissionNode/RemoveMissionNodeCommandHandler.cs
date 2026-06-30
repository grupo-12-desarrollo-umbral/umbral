using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.RemoveMissionNode;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.RemoveMissionNode;

public sealed class RemoveMissionNodeCommandHandler
    : IRequestHandler<RemoveMissionNodeCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public RemoveMissionNodeCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(RemoveMissionNodeCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        MissionStructureEditor.RemoveNode(mission, request.NodeId);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
