using Microsoft.EntityFrameworkCore;
using Npgsql;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

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
            .Include(session => session.TreasureEvidenceSubmissions)
            .Include(session => session.SessionEvents)
            .Include("_clueReleaseRecords")
            .Include("_operativeClues")
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
            .Include(session => session.TreasureEvidenceSubmissions)
            .Include("_clueReleaseRecords")
            .Include("_operativeClues")
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
        // window, and the mission deadline a treasure-hunt substage displays in place of a window of
        // its own. The mission timer is seeded for every session at start, so its branch alone matches
        // every started session; the in-memory pass below narrows it to the substages that display it.
        //
        // The predicate below stays within translatable timer columns so EF never has to translate the
        // owned-collection snapshot navigation into SQL. The TreasureHunt scoping is then applied in
        // memory over the hydrated snapshot (deep-loaded via the Includes below).
        var candidates = await _context.LiveSessions
            .Where(session =>
                session.State == SessionState.Active &&
                ((session.ActiveQuestionIndex != null &&
                    EF.Property<DateTimeOffset?>(session, "_questionTimerExpiredAt") == null) ||
                 (EF.Property<DateTimeOffset?>(session, "_missionTimerAdvancingSince") != null &&
                    EF.Property<DateTimeOffset?>(session, "_missionTimerExpiredAt") == null) ||
                 // A reveal-pending session (HU-35) has no active-question window ticking,
                 // but must still be ticked so the deferred next-question activation fires on time.
                 EF.Property<DateTimeOffset?>(session, "_questionRevealUntil") != null ||
                 // Likewise a substage-end ranking reveal (D-3): nothing is ticking behind it, but the
                 // deferred advance/finish fires when its deadline elapses.
                 EF.Property<DateTimeOffset?>(session, "_substageRevealUntil") != null))
            .Include(session => session.MissionRuntimeSnapshot)
                .ThenInclude(snapshot => snapshot.StageSnapshots)
                    .ThenInclude(stage => stage.SubstageSnapshots)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return candidates
            .Where(session =>
                session.ActiveQuestionIndex != null ||
                session.IsAwaitingQuestionReveal ||
                session.IsAwaitingSubstageRankingReveal ||
                ActiveSubstageIsTreasureHunt(session))
            .ToList();
    }

    // True when the session's active substage (matched by ActiveSubstageId) resolves, within the
    // hydrated snapshot, to a TreasureHunt substage — the only substage that ticks on the mission
    // timer alone, having no question window.
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
        var entry = _context.Entry(liveSession);
        if (entry.State == EntityState.Detached)
        {
            _context.LiveSessions.Add(liveSession);
        }
        else
        {
            // Force a principal-row UPDATE on every save so the aggregate root's xmin token guards the
            // whole aggregate, not only mutations that happen to touch a principal column. A child-only
            // write — a target scan, a team join, an answer — otherwise emits no UPDATE on live_sessions,
            // so xmin is never compared and two writers loaded from the same token both win. Marking
            // LastModified modified makes EF emit `UPDATE live_sessions SET last_modified = @now WHERE
            // id = @id AND xmin = @original`; the audit interceptor then overwrites @now with the real
            // timestamp during SaveChanges. Only the second writer's stale xmin matches no row, surfacing
            // as the DbUpdateConcurrencyException the catch below turns into a recoverable conflict.
            entry.Property(session => session.LastModified).IsModified = true;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The xmin token moved: someone committed to live_sessions between our read and our write.
            // Since UpdateAsync now forces a principal-row UPDATE on every save, this arm catches every
            // cross-writer race on the aggregate, including child-only mutations that touch no principal
            // column of their own.
            throw new ConcurrentModificationException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Defense-in-depth beneath the xmin token: every uniqueness rule on this aggregate is
            // enforced in the domain first, so a duplicate that reaches Postgres is by definition one
            // that was not visible when the domain checked — i.e. a concurrent writer won. Translating
            // to a concurrency loss lets the retry re-read and reach the domain's own verdict (a 409 for
            // a duplicate answer, a rejected submission for a duplicate scan) rather than surfacing the
            // raw 23505 as a 500.
            throw new ConcurrentModificationException(exception);
        }
    }
}
