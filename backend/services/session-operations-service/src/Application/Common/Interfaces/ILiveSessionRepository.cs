using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface ILiveSessionRepository
{
    Task<LiveSession?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task UpdateAsync(LiveSession liveSession, CancellationToken cancellationToken);
}
