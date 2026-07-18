using Microsoft.EntityFrameworkCore;
using Npgsql;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class ScoreEntryRepository : IScoreEntryRepository
{
    private readonly ScoringMonitoringDbContext _context;

    public ScoreEntryRepository(ScoringMonitoringDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ScoreEntry scoreEntry, CancellationToken cancellationToken)
    {
        _context.ScoreEntries.Add(scoreEntry);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The handler guards on ExistsForSourceAsync, but that read and this write are not atomic:
            // under at-least-once delivery two consumers can process the same source event at once, both
            // find no entry, and the loser collides on the (source_entity_type, source_entity_id) unique
            // index. Recording a source exactly once is the guard's whole purpose, so a lost race means
            // the entry already exists — the idempotent outcome is a no-op, not a raw 23505 escaping into
            // Application. Detach the rejected insert so it cannot be reapplied on a later SaveChanges.
            _context.Entry(scoreEntry).State = EntityState.Detached;
        }
    }

    public Task<bool> ExistsForSourceAsync(
        ScoreSourceType sourceEntityType,
        Guid sourceEntityId,
        CancellationToken cancellationToken)
    {
        return _context.ScoreEntries.AnyAsync(
            scoreEntry => scoreEntry.SourceEntityType == sourceEntityType
                && scoreEntry.SourceEntityId == sourceEntityId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreEntry>> ListByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return await _context.ScoreEntries
            .AsNoTracking()
            .Where(scoreEntry => scoreEntry.LiveSessionId == liveSessionId)
            .OrderBy(scoreEntry => scoreEntry.RecordedAt)
            .ThenBy(scoreEntry => scoreEntry.ScoreEntryId)
            .ToListAsync(cancellationToken);
    }
}
