using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Missions.Commands.AssignSubstagePlayMode;

public sealed class AssignSubstagePlayModeCommandHandler
    : IRequestHandler<AssignSubstagePlayModeCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public AssignSubstagePlayModeCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(AssignSubstagePlayModeCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);

        MissionStructureEditor.AssignPlayMode(
            mission,
            request.StageId,
            request.SubstageId,
            Enum.Parse<SubstagePlayMode>(request.PlayMode));

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
