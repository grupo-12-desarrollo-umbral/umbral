using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Common.Interfaces;

public interface IRankingRepository
{
    Task<Ranking?> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task SaveAsync(Ranking ranking, CancellationToken cancellationToken);
}
