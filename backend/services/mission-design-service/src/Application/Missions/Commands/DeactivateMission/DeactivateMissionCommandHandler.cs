using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Missions.Commands.DeactivateMission;

namespace umbral_backend.Application.Missions.Commands.DeactivateMission;

public sealed class DeactivateMissionCommandHandler : IRequestHandler<DeactivateMissionCommand>
{
    private readonly IMissionRepository _missionRepository;
    private readonly IClock _clock;

    public DeactivateMissionCommandHandler(IMissionRepository missionRepository, IClock clock)
    {
        _missionRepository = missionRepository;
        _clock = clock;
    }

    public async Task Handle(DeactivateMissionCommand request, CancellationToken cancellationToken)
    {
        var mission = await _missionRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Mission", request.Id);

        mission.Deactivate(_clock.UtcNow);

        await _missionRepository.UpdateAsync(mission, cancellationToken);
    }
}
