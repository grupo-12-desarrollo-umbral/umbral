using Microsoft.EntityFrameworkCore;
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
        await _context.SaveChangesAsync(cancellationToken);
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
