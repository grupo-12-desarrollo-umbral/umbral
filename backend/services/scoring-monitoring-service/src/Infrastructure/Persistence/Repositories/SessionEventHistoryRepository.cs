using Microsoft.EntityFrameworkCore;
using Npgsql;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Infrastructure.Persistence.Configurations;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class SessionEventHistoryRepository : ISessionEventHistoryRepository
{
    private readonly ScoringMonitoringDbContext _context;

    public SessionEventHistoryRepository(ScoringMonitoringDbContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(SessionEvent sessionEvent, CancellationToken cancellationToken)
    {
        var alreadyRecorded = await _context.SessionEvents
            .AsNoTracking()
            .AnyAsync(existing => existing.SourceEventKey == sessionEvent.SourceEventKey, cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        _context.SessionEvents.Add(sessionEvent);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: SessionEventConfiguration.SourceEventKeyIndexName
            })
        {
            _context.Entry(sessionEvent).State = EntityState.Detached;
        }
    }
}
