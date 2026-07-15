using Microsoft.EntityFrameworkCore;
using Npgsql;
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

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // At-least-once delivery: between the existence check above and this SaveChanges, a
                // concurrent delivery inserted the row (a TOCTOU race on PK_evidence_trace_entries —
                // read-then-Add is not atomic). Without this catch the loser dead-letters to
                // <consumer>_error. Detach the rejected insert so the scoped DbContext is usable, then
                // fall through and merge onto the winner's row: the row existing is not sufficient,
                // because this delivery may carry the half the winner lacks.
                _context.Entry(entry).State = EntityState.Detached;

                existing = await _context.EvidenceTraceEntries
                    .SingleAsync(e => e.EvidenceSubmissionId == entry.EvidenceSubmissionId, cancellationToken);
            }
        }

        // A caller that mutated the tracked instance (the resolution path) passes it straight back;
        // one that built a fresh entry (the registration path) needs its values folded in — that
        // second case wrote nothing at all before, silently losing the registration context whenever
        // a resolution created the row first.
        if (!ReferenceEquals(existing, entry))
        {
            existing.MergeFrom(entry);
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
