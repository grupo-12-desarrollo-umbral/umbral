using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class SessionAssignmentRepository : ISessionAssignmentReadRepository, ISessionAssignmentProjectionRepository
{
    private readonly ScoringMonitoringDbContext _context;

    public SessionAssignmentRepository(ScoringMonitoringDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetAssignedOperatorUserIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var assignment = await _context.SessionOperatorAssignments
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.LiveSessionId == liveSessionId, cancellationToken);

        return assignment?.AssignedOperatorUserId;
    }

    public async Task UpsertAsync(Guid liveSessionId, Guid assignedOperatorUserId, CancellationToken cancellationToken)
    {
        var existing = await _context.SessionOperatorAssignments
            .SingleOrDefaultAsync(a => a.LiveSessionId == liveSessionId, cancellationToken);

        if (existing is null)
        {
            _context.SessionOperatorAssignments.Add(new SessionOperatorAssignmentProjection
            {
                LiveSessionId = liveSessionId,
                AssignedOperatorUserId = assignedOperatorUserId
            });
        }
        else
        {
            existing.AssignedOperatorUserId = assignedOperatorUserId;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
