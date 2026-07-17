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
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
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

        // A trivia substage ticks the active-question window; a treasure-hunt substage has no window of
        // its own and ticks the mission deadline. ActiveQuestionIndex distinguishes them (only trivia
        // carries one), which matches the ListActiveTimersAsync predicate branches.
        var isTriviaQuestion = liveSession.ActiveQuestionIndex is not null;
        var wasAdvancing = isTriviaQuestion
            ? liveSession.IsQuestionTimerAdvancing
            : liveSession.IsMissionTimerAdvancing;

        var snapshot = isTriviaQuestion
            ? liveSession.MarkQuestionTimerExpiredIfElapsed(now)
            : liveSession.MarkMissionTimerExpiredIfElapsed(now);

        await broadcaster.BroadcastTimerUpdatedAsync(
            CreateNotification(liveSession, snapshot, now),
            cancellationToken);

        if (!wasAdvancing || !snapshot.IsExpired)
        {
            return;
        }

        // Persist the freshly-expired window so an exhausted timer stops re-ticking every second.
        await repository.UpdateAsync(liveSession, cancellationToken);

        // Report-only on expiry for treasure-hunt: those substages advance by target resolution
        // (HU-29..32), not by the timer, so we broadcast Expired and stop. Only a trivia active
        // question auto-closes and advances.
        if (!isTriviaQuestion)
        {
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
        DateTimeOffset emittedAt)
    {
        var missionSnapshot = liveSession.HasMissionDeadline
            ? liveSession.GetMissionTimerSnapshot(emittedAt)
            : null;

        return new SessionTimerUpdatedNotificationDto(
            liveSession.LiveSessionId,
            ToWholeMilliseconds(snapshot.RemainingDuration),
            liveSession.State == SessionState.Paused,
            emittedAt,
            ToWholeMilliseconds(snapshot.TotalDuration),
            snapshot.IsExpired,
            liveSession.State.ToString(),
            missionSnapshot is null ? null : ToWholeMilliseconds(missionSnapshot.RemainingDuration),
            missionSnapshot is null ? null : ToWholeMilliseconds(missionSnapshot.TotalDuration));
    }

    private static long ToWholeMilliseconds(TimeSpan duration)
    {
        return (long)Math.Ceiling(duration.TotalMilliseconds);
    }
}
