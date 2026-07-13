using Microsoft.EntityFrameworkCore;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
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
            .Include(session => session.TriviaAnswerSubmissions)
            .Include("_clueReleaseRecords")
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.StageSnapshots)
                    .ThenInclude(stage => stage.SubstageSnapshots)
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TargetSnapshots)
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TriviaQuestionSnapshots)
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
            .Include(session => session.TriviaAnswerSubmissions)
            .Include("_clueReleaseRecords")
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.StageSnapshots)
                    .ThenInclude(stage => stage.SubstageSnapshots)
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TargetSnapshots)
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TriviaQuestionSnapshots)
                    .ThenInclude(question => question.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(session => session.SessionCode == normalizedCode, cancellationToken);
    }

    public Task<LiveSession?> GetTimerSessionByIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return _context.LiveSessions
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.TriviaQuestionSnapshots)
                    .ThenInclude(question => question.Options)
            .AsSplitQuery()
            .SingleOrDefaultAsync(session => session.LiveSessionId == liveSessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionOperatorSummaryDto>> ListAssignableSummariesAsync(
        int? assignedOperatorUserId,
        bool includeConcluded,
        CancellationToken cancellationToken)
    {
        var query = _context.LiveSessions
            .AsNoTracking()
            .OrderByDescending(session => session.ScheduledAt);

        if (!includeConcluded)
        {
            query = query.Where(session =>
                    session.State != SessionState.Finished && session.State != SessionState.Cancelled)
                .OrderByDescending(session => session.ScheduledAt);
        }

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
        // Two authoritative-timer windows tick under an Active session: the trivia active-question
        // window, and the treasure-hunt substage window. The substage branch is scoped to the active
        // substage actually being TreasureHunt — the _substageTimer* fields can be left stale after a
        // TreasureHunt -> Trivia advance (SeedSubstageTimerIfTreasureHunt early-returns without clearing
        // them), so the advancing/expired predicate alone would keep ticking such sessions redundantly.
        //
        // The predicate below stays within translatable timer columns so EF never has to translate the
        // owned-collection snapshot navigation into SQL. The TreasureHunt scoping is then applied in
        // memory over the hydrated snapshot (deep-loaded via the Includes below).
        var candidates = await _context.LiveSessions
            .Where(session =>
                session.State == SessionState.Active &&
                ((session.ActiveQuestionIndex != null &&
                    EF.Property<DateTimeOffset?>(session, "_questionTimerExpiredAt") == null) ||
                 (EF.Property<DateTimeOffset?>(session, "_substageTimerAdvancingSince") != null &&
                    EF.Property<DateTimeOffset?>(session, "_substageTimerExpiredAt") == null)))
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.StageSnapshots)
                    .ThenInclude(stage => stage.SubstageSnapshots)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return candidates
            .Where(session =>
                session.ActiveQuestionIndex != null || ActiveSubstageIsTreasureHunt(session))
            .ToList();
    }

    // True when the session's active substage (matched by ActiveSubstageId) resolves, within the
    // hydrated snapshot, to a TreasureHunt substage. Guards against stale _substageTimer* fields left
    // behind by a TreasureHunt -> Trivia advance.
    private static bool ActiveSubstageIsTreasureHunt(LiveSession session)
    {
        return session.MissionRuntimeSnapshot is not null &&
            session.MissionRuntimeSnapshot.StageSnapshots
                .SelectMany(stage => stage.SubstageSnapshots)
                .Any(substage =>
                    substage.SubstageSnapshotId == session.ActiveSubstageId &&
                    substage.PlayMode == SubstagePlayMode.TreasureHunt);
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
