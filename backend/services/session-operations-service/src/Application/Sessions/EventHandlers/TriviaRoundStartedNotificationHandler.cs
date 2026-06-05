using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

public sealed class TriviaRoundStartedNotificationHandler : INotificationHandler<SessionStateChangedEvent>
{
    private const int PreGameCountdownSeconds = 5;

    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ISessionTimerBroadcaster _sessionTimerBroadcaster;
    private readonly ITriviaRoundOrchestratorFacade _triviaRoundOrchestratorFacade;
    private readonly TimeProvider _timeProvider;

    public TriviaRoundStartedNotificationHandler(
        ILiveSessionRepository liveSessionRepository,
        ISessionTimerBroadcaster sessionTimerBroadcaster,
        ITriviaRoundOrchestratorFacade triviaRoundOrchestratorFacade,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _sessionTimerBroadcaster = sessionTimerBroadcaster;
        _triviaRoundOrchestratorFacade = triviaRoundOrchestratorFacade;
        _timeProvider = timeProvider;
    }

    public async Task Handle(SessionStateChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.CurrentState != SessionState.Active)
        {
            return;
        }

        var session = await _liveSessionRepository.GetByIdAsync(notification.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(umbral_backend.Domain.Entities.LiveSession), notification.LiveSessionId.ToString());

        if (session.SessionMode != SessionMode.Trivia)
        {
            return;
        }

        for (var remainingSeconds = PreGameCountdownSeconds; remainingSeconds >= 1; remainingSeconds--)
        {
            var emittedAt = _timeProvider.GetUtcNow();
            await _sessionTimerBroadcaster.BroadcastTimerUpdatedAsync(
                new SessionTimerUpdatedNotificationDto(
                    session.LiveSessionId,
                    RemainingMilliseconds: remainingSeconds * 1000L,
                    IsPaused: false,
                    EmittedAt: emittedAt,
                    TotalMilliseconds: PreGameCountdownSeconds * 1000L,
                    IsExpired: false,
                    SessionState: notification.CurrentState.ToString()),
                cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);
        }

        await _triviaRoundOrchestratorFacade.ActivateNextQuestionAsync(
            session,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
