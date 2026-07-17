using Microsoft.EntityFrameworkCore;
using Npgsql;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class RankingRepository : IRankingRepository
{
    private readonly ScoringMonitoringDbContext _context;

    public RankingRepository(ScoringMonitoringDbContext context)
    {
        _context = context;
    }

    public Task<Ranking?> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return _context.Rankings
            .Include(ranking => ranking.Rows.OrderBy(row => row.Position).ThenBy(row => row.TeamId))
            .SingleOrDefaultAsync(ranking => ranking.LiveSessionId == liveSessionId, cancellationToken);
    }

    public async Task SaveAsync(Ranking ranking, CancellationToken cancellationToken)
    {
        if (_context.Entry(ranking).State == EntityState.Detached)
        {
            _context.Rankings.Add(ranking);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The xmin token moved: another recalculation committed to this ranking's row between our
            // read and our write. Translating to the scoring-owned conflict lets the retry re-read the
            // winner's row and derive the next CalculationVersion from it, rather than surfacing a 500.
            throw new ConcurrentRankingModificationException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two consumers recalculated the first ranking for one session at once: both found no row,
            // both INSERTed, and the second collides on the rankings.live_session_id unique index. That
            // is a concurrency loss like any other — the retry re-reads the row the winner created and
            // refreshes it, so the session ends with exactly one ranking rather than a raw 23505.
            throw new ConcurrentRankingModificationException(exception);
        }
    }
}
