using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Interfaces;

public interface IScoreEntryRepository
{
    Task AddAsync(ScoreEntry scoreEntry, CancellationToken cancellationToken);

    Task<bool> ExistsForSourceAsync(
        ScoreSourceType sourceEntityType,
        Guid sourceEntityId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoreEntry>> ListByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
