using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Infrastructure.Persistence.Repositories;

public sealed class LiveSessionRepository : ILiveSessionRepository
{
    private readonly ApplicationDbContext _context;

    public LiveSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<LiveSession?> GetByIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return _context.LiveSessions
            .Include(session => session.Teams)
                .ThenInclude(team => team.Members)
            .Include(session => session.Participants)
            .Include(session => session.JoinContexts)
            .Include(session => session.TriviaSnapshot!)
                .ThenInclude(snapshot => snapshot.Questions)
                    .ThenInclude(question => question.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(session => session.LiveSessionId == liveSessionId, cancellationToken);
    }

    public Task<LiveSession?> GetBySessionCodeAsync(string sessionCode, CancellationToken cancellationToken)
    {
        var normalizedCode = sessionCode.Trim().ToUpperInvariant();

        return _context.LiveSessions
            .Include(session => session.Teams)
                .ThenInclude(team => team.Members)
            .Include(session => session.Participants)
            .Include(session => session.JoinContexts)
            .Include(session => session.TriviaSnapshot!)
                .ThenInclude(snapshot => snapshot.Questions)
                    .ThenInclude(question => question.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(session => session.SessionCode == normalizedCode, cancellationToken);
    }

    public Task<LiveSession?> GetTimerSessionByIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return _context.LiveSessions
            .Include(session => session.TriviaSnapshot!)
                .ThenInclude(snapshot => snapshot.Questions)
                    .ThenInclude(question => question.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(session => session.LiveSessionId == liveSessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionOperatorSummaryDto>> ListAssignableSummariesAsync(
        int? assignedOperatorUserId,
        CancellationToken cancellationToken)
    {
        var query = _context.LiveSessions
            .AsNoTracking()
            .Where(session => session.State != SessionState.Finished && session.State != SessionState.Cancelled)
            .OrderByDescending(session => session.ScheduledAt);

        if (assignedOperatorUserId is not null)
        {
            query = query.Where(session => session.AssignedOperatorUserId == assignedOperatorUserId.Value)
                .OrderByDescending(session => session.ScheduledAt);
        }

        var rows = await query
            .Select(session => new
            {
                session.LiveSessionId,
                session.SessionCode,
                Title = session.TitleSnapshot,
                session.State,
                session.AssignedOperatorUserId,
                session.ScheduledAt,
                session.LastStateChangedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SessionOperatorSummaryDto(
                row.LiveSessionId,
                row.SessionCode,
                row.Title,
                row.State.ToString(),
                row.AssignedOperatorUserId,
                row.ScheduledAt,
                row.LastStateChangedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<LiveSession>> ListActiveTimersAsync(CancellationToken cancellationToken)
    {
        return await _context.LiveSessions
            .Where(session =>
                session.State == SessionState.Active &&
                (
                    (
                        EF.Property<DateTimeOffset?>(session, "_sessionTimerAdvancingSince") != null &&
                        EF.Property<DateTimeOffset?>(session, "_sessionTimerExpiredAt") == null
                    ) ||
                    (
                        session.ActiveQuestionIndex != null &&
                        EF.Property<DateTimeOffset?>(session, "_questionTimerExpiredAt") == null
                    )
                ))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(LiveSession liveSession, CancellationToken cancellationToken)
    {
        if (_context.Entry(liveSession).State == EntityState.Detached)
        {
            _context.LiveSessions.Add(liveSession);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
