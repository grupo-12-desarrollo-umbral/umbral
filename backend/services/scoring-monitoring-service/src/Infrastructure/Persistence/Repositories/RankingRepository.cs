using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.ValueObjects;

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

    public async Task<IReadOnlyDictionary<Guid, ResolutionTime>> GetResolutionTimesByLiveSessionIdAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var rows = await _context.Rankings
            .AsNoTracking()
            .Where(ranking => ranking.LiveSessionId == liveSessionId)
            .SelectMany(ranking => ranking.Rows)
            .Select(row => new
            {
                row.TeamId,
                row.ResolutionTime
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.TeamId, row => row.ResolutionTime);
    }

    public async Task SaveAsync(Ranking ranking, CancellationToken cancellationToken)
    {
        if (_context.Entry(ranking).State == EntityState.Detached)
        {
            _context.Rankings.Add(ranking);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
