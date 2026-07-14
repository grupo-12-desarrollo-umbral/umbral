using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.Common.Interfaces;

public interface IRankingRepository
{
    Task<Ranking?> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ResolutionTime>> GetResolutionTimesByLiveSessionIdAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken);

    Task SaveAsync(Ranking ranking, CancellationToken cancellationToken);
}
