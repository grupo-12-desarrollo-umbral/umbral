using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.AddTarget;
using umbral_backend.Application.Missions.Common;

namespace umbral_backend.Application.Missions.Commands.AddTarget;

public sealed class AddTargetCommandHandler
    : IRequestHandler<AddTargetCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public AddTargetCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(AddTargetCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.MissionId, cancellationToken)
            ?? throw new NotFoundException("Mission", request.MissionId);
        MissionStructureEditor.FindSubstage(mission, request.StageId, request.SubstageId);

        mission.AddTarget(
            request.StageId,
            request.SubstageId,
            request.Name,
            request.QrCode,
            request.SequenceOrder,
            request.IsActive);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
        return MissionDtoMapper.Map(mission);
    }
}
