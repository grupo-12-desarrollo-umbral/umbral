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
            var wasAdvancing = liveSession.IsQuestionTimerAdvancing;
            var snapshot = liveSession.MarkQuestionTimerExpiredIfElapsed(now);

            await broadcaster.BroadcastTimerUpdatedAsync(
                CreateNotification(liveSession, snapshot, now),
                cancellationToken);

            if (!wasAdvancing || !snapshot.IsExpired)
            {
                continue;
            }

            await repository.UpdateAsync(liveSession, cancellationToken);

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
