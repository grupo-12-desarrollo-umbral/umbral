using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface IMissionRuntimeSource
{
    Task<MissionRuntimeDto?> GetByIdAsync(int missionId, CancellationToken cancellationToken);
}
