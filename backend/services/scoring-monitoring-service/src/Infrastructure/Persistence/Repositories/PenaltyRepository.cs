using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class PenaltyRepository : IPenaltyRepository
{
    private readonly ScoringMonitoringDbContext _context;

    public PenaltyRepository(ScoringMonitoringDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Penalty penalty, CancellationToken cancellationToken)
    {
        _context.Set<Penalty>().Add(penalty);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
