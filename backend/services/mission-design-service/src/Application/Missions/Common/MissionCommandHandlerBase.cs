using NotFoundException = umbral_backend.Application.Common.Exceptions.NotFoundException;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Missions.Common;

public abstract class MissionCommandHandlerBase
{
    protected MissionCommandHandlerBase(IMissionRepository missionRepository)
    {
        MissionRepository = missionRepository;
    }

    protected IMissionRepository MissionRepository { get; }

    protected async Task<Mission> GetMissionAsync(int missionId, CancellationToken cancellationToken)
    {
        return await MissionRepository.GetByIdAsync(missionId, cancellationToken)
            ?? throw new NotFoundException("Mission", missionId);
    }

    protected async Task UpdateAndReturnAsync(Mission mission, CancellationToken cancellationToken)
    {
        await MissionRepository.UpdateAsync(mission, cancellationToken);
    }
}
