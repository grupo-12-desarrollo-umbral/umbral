using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface IMissionReadinessSource
{
    Task<MissionReadinessDto?> GetByIdAsync(int missionId, CancellationToken cancellationToken);
}
