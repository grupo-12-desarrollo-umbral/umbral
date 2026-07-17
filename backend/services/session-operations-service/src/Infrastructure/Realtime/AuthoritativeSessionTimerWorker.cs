using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Infrastructure.Realtime;

public sealed class AuthoritativeSessionTimerWorker : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthoritativeSessionTimerWorker> _logger;

    public AuthoritativeSessionTimerWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<AuthoritativeSessionTimerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Authoritative session timer tick failed.");
            }
        }
    }

    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILiveSessionRepository>();
        var broadcaster = scope.ServiceProvider.GetRequiredService<ISessionTimerBroadcaster>();
        var triviaRoundOrchestratorFacade = scope.ServiceProvider.GetRequiredService<ITriviaRoundOrchestratorFacade>();
        var substageAdvanceCoordinator = scope.ServiceProvider.GetRequiredService<ISubstageAdvanceCoordinator>();
        var now = _timeProvider.GetUtcNow();

        var liveSessions = await repository.ListActiveTimersAsync(cancellationToken);
        foreach (var liveSession in liveSessions)
        {
            try
            {
                await TickSessionAsync(
                    liveSession,
                    repository,
                    broadcaster,
                    triviaRoundOrchestratorFacade,
                    substageAdvanceCoordinator,
                    now,
                    cancellationToken);
            }
            catch (ConcurrentModificationException exception)
            {
                // A request-path writer committed to this session mid-tick. The worker has no retry
                // (it is outside the MediatR pipeline) and needs none: every tick recomputes from
                // stored state, so the next one re-reads and re-marks whatever this tick abandoned.
                //
                // Clearing tracking is what keeps the *batch* alive. One context is shared by every
                // session in the tick, so a single SaveChanges covers them all — leaving the losing
                // session tracked would fail every subsequent session's save for the same reason.
                // The mutations discarded alongside it are recomputed next tick.
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ResetTracking();

                _logger.LogDebug(
                    exception,
                    "Concurrency loss ticking session {LiveSessionId}; deferring to the next tick.",
                    liveSession.LiveSessionId);
            }
        }
    }

    private async Task TickSessionAsync(
        LiveSession liveSession,
        ILiveSessionRepository repository,
        ISessionTimerBroadcaster broadcaster,
        ITriviaRoundOrchestratorFacade triviaRoundOrchestratorFacade,
        ISubstageAdvanceCoordinator substageAdvanceCoordinator,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // A substage has ended and its ranking is on screen (D-3). Checked FIRST, ahead of mission
        // expiry, which is what implements D-6: a deadline landing mid-reveal lets the reveal play out
        // and the session ends on it, rather than flashing a screen for a fraction of a second. The
        // ranking is already the terminal display, so that is visually identical to a normal end.
        //
        // No timer tick is broadcast meanwhile: the ranking must not move while displayed (D-2), and
        // both clocks are frozen or irrelevant behind it.
        if (liveSession.IsAwaitingSubstageRankingReveal)
        {
            if (liveSession.IsSubstageRevealElapsed(now))
            {
                await substageAdvanceCoordinator.CompleteRankingRevealAsync(
                    liveSession,
                    now,
                    cancellationToken);
            }

            return;
        }

        // A just-closed trivia question is in its reveal window (HU-35): the question is closed but
        // the next activation is deferred so participants see the result. Advance to the next
        // question / substage once the reveal deadline passes; no timer tick is broadcast meanwhile
        // (the question timer is already frozen at expired).
        if (liveSession.IsAwaitingQuestionReveal)
        {
            if (liveSession.IsQuestionRevealElapsed(now))
            {
                await triviaRoundOrchestratorFacade.CompleteQuestionRevealAsync(
                    liveSession,
                    now,
                    cancellationToken);
            }

            return;
        }

        var wasQuestionAdvancing = liveSession.IsQuestionTimerAdvancing;
        var wasMissionAdvancing = liveSession.IsMissionTimerAdvancing;

        // The mission deadline ticks unconditionally, in both play modes (D-4): it is one budget for
        // the whole mission and expires wherever play happens to be.
        var missionSnapshot = liveSession.HasMissionDeadline
            ? liveSession.MarkMissionTimerExpiredIfElapsed(now)
            : null;

        // ActiveQuestionIndex distinguishes the play modes (only trivia carries one) and now selects
        // only WHICH window fills RemainingMilliseconds — not which clock ticks. A trivia substage
        // reports its active-question window; a treasure hunt has none of its own and mirrors the
        // mission deadline (D-5).
        var isTriviaQuestion = liveSession.ActiveQuestionIndex is not null;
        var snapshot = isTriviaQuestion
            ? liveSession.MarkQuestionTimerExpiredIfElapsed(now)
            : missionSnapshot ?? liveSession.GetMissionTimerSnapshot(now);

        await broadcaster.BroadcastTimerUpdatedAsync(
            CreateNotification(liveSession, snapshot, missionSnapshot, now),
            cancellationToken);

        var missionExpired = wasMissionAdvancing && missionSnapshot is { IsExpired: true };
        var questionExpired = isTriviaQuestion && wasQuestionAdvancing && snapshot.IsExpired;

        if (!missionExpired && !questionExpired)
        {
            return;
        }

        // Persist the freshly-expired window so an exhausted timer stops re-ticking every second.
        await repository.UpdateAsync(liveSession, cancellationToken);

        // MaximumTime ran out: the mission ends where it stands, in either play mode (D-4). This
        // replaces the report-only dead end that made a treasure-hunt substage's pointer immovable —
        // and it takes precedence over the question close below, since there is no next question to
        // open on a mission that is over.
        if (missionExpired)
        {
            await substageAdvanceCoordinator.FinishOnMissionDeadlineAsync(
                liveSession,
                now,
                cancellationToken);
            return;
        }

        await triviaRoundOrchestratorFacade.CloseAndAdvanceAsync(
            liveSession,
            now,
            cancellationToken);
    }

    // The mission deadline rides every tick alongside the window the substage owns (D-5), so both
    // clocks reach the client together. During a treasure hunt the two are the same snapshot: that
    // substage has no window of its own and displays the deadline itself.
    private static SessionTimerUpdatedNotificationDto CreateNotification(
        LiveSession liveSession,
        AuthoritativeSessionTimerSnapshot snapshot,
        AuthoritativeSessionTimerSnapshot? missionSnapshot,
        DateTimeOffset emittedAt)
    {
        return new SessionTimerUpdatedNotificationDto(
            liveSession.LiveSessionId,
            ToWholeMilliseconds(snapshot.RemainingDuration),
            liveSession.State == SessionState.Paused,
            emittedAt,
            ToWholeMilliseconds(snapshot.TotalDuration),
            snapshot.IsExpired,
            liveSession.State.ToString(),
            missionSnapshot is null ? null : ToWholeMilliseconds(missionSnapshot.RemainingDuration),
            missionSnapshot is null ? null : ToWholeMilliseconds(missionSnapshot.TotalDuration),
            // A real substage/deadline window, never the pre-game countdown — so a short (5–10s)
            // question window is not mistaken for the pre-round numeral by clients.
            IsPregameCountdown: false);
    }

    private static long ToWholeMilliseconds(TimeSpan duration)
    {
        return (long)Math.Ceiling(duration.TotalMilliseconds);
    }
}
