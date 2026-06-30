using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.CreateMission;
using umbral_backend.Application.Missions.Common;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandHandler : IRequestHandler<CreateMissionCommand, MissionDto>
{
    private readonly IMissionRepository _missionRepository;

    public CreateMissionCommandHandler(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
    }

    public async Task<MissionDto> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = Mission.Create(
            request.Name,
            request.Description,
            request.Difficulty,
            request.MaximumTimeMinutes);

        await _missionRepository.AddAsync(mission, cancellationToken);

        return MissionDtoMapper.Map(mission);
    }
}
