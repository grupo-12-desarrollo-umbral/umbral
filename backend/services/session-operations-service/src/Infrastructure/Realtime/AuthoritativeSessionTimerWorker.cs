using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
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

                continue;
            }

            // A trivia substage ticks the active-question window; a treasure-hunt substage ticks the
            // substage window. ActiveQuestionIndex distinguishes them (only trivia carries one), which
            // matches the ListActiveTimersAsync predicate branches.
            var isTriviaQuestion = liveSession.ActiveQuestionIndex is not null;
            var wasAdvancing = isTriviaQuestion
                ? liveSession.IsQuestionTimerAdvancing
                : liveSession.IsSubstageTimerAdvancing;

            var snapshot = isTriviaQuestion
                ? liveSession.MarkQuestionTimerExpiredIfElapsed(now)
                : liveSession.MarkSubstageTimerExpiredIfElapsed(now);

            await broadcaster.BroadcastTimerUpdatedAsync(
                CreateNotification(liveSession, snapshot, now),
                cancellationToken);

            if (!wasAdvancing || !snapshot.IsExpired)
            {
                continue;
            }

            // Persist the freshly-expired window so an exhausted timer stops re-ticking every second.
            await repository.UpdateAsync(liveSession, cancellationToken);

            // Report-only on expiry for treasure-hunt: those substages advance by target resolution
            // (HU-29..32), not by the timer, so we broadcast Expired and stop. Only a trivia active
            // question auto-closes and advances.
            if (!isTriviaQuestion)
            {
                continue;
            }

            await triviaRoundOrchestratorFacade.CloseAndAdvanceAsync(
                liveSession,
                now,
                cancellationToken);
        }
    }

    private static SessionTimerUpdatedNotificationDto CreateNotification(
        LiveSession liveSession,
        AuthoritativeSessionTimerSnapshot snapshot,
        DateTimeOffset emittedAt)
    {
        return new SessionTimerUpdatedNotificationDto(
            liveSession.LiveSessionId,
            ToWholeMilliseconds(snapshot.RemainingDuration),
            liveSession.State == SessionState.Paused,
            emittedAt,
            ToWholeMilliseconds(snapshot.TotalDuration),
            snapshot.IsExpired,
            liveSession.State.ToString());
    }

    private static long ToWholeMilliseconds(TimeSpan duration)
    {
        return (long)Math.Ceiling(duration.TotalMilliseconds);
    }
}
