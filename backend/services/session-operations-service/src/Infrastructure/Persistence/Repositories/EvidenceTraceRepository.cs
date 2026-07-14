using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class EvidenceTraceRepository : IEvidenceTraceRepository
{
    private readonly ApplicationDbContext _context;

    public EvidenceTraceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EvidenceTraceEntry?> GetByEvidenceSubmissionIdAsync(
        Guid evidenceSubmissionId,
        CancellationToken cancellationToken)
    {
        return await _context.EvidenceTraceEntries
            .SingleOrDefaultAsync(entry => entry.EvidenceSubmissionId == evidenceSubmissionId, cancellationToken);
    }

    public async Task UpsertAsync(EvidenceTraceEntry entry, CancellationToken cancellationToken)
    {
        var existing = await _context.EvidenceTraceEntries
            .SingleOrDefaultAsync(e => e.EvidenceSubmissionId == entry.EvidenceSubmissionId, cancellationToken);

        if (existing is null)
        {
            _context.EvidenceTraceEntries.Add(entry);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EvidenceTraceEntry>> ListBySessionAsync(
        Guid liveSessionId,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        var query = _context.EvidenceTraceEntries
            .AsNoTracking()
            .Where(entry => entry.LiveSessionId == liveSessionId);

        if (teamId.HasValue)
        {
            query = query.Where(entry => entry.TeamId == teamId.Value);
        }

        return await query
            .OrderBy(entry => entry.SubmittedAt)
            .ToListAsync(cancellationToken);
    }
}
