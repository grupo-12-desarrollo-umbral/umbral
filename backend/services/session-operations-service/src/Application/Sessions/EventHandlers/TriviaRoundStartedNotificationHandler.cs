using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
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

        // Resume (Paused → Active) must NOT replay the pre-game countdown. The countdown is a go-live /
        // substage-entry affordance, not a resume one: re-running it here broadcasts a spurious 5s
        // "Prepárate" round to operators who merely un-paused, and — because a pause landing mid-countdown
        // never cancels the original loop — can overlap two countdowns and double-activate the question.
        // The authoritative timer worker already re-emits the frozen substage/mission window on resume.
        if (notification.PreviousState == SessionState.Paused)
        {
            return;
        }

        var session = await _liveSessionRepository.GetByIdAsync(notification.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(umbral_backend.Domain.Entities.LiveSession), notification.LiveSessionId.ToString());

        // Entering Active already set ActiveSubstageId to the first substage (domain). Auto-activation
        // is trivia-only (backend/adr/0005-substage-advancement-pointer-and-timer-driven-orchestration.md):
        // a treasure-hunt first substage parks — no countdown, no question. The facade owns the
        // activate flow; this handler stays thin.
        if (session.State != SessionState.Active
            || session.ActiveQuestionIndex is not null
            || !ActiveSubstageIsTrivia(session))
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
                    SessionState: notification.CurrentState.ToString(),
                    IsPregameCountdown: true),
                cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellationToken);
        }

        await _triviaRoundOrchestratorFacade.ActivateNextQuestionAsync(
            session,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static bool ActiveSubstageIsTrivia(LiveSession session)
    {
        return session.MissionRuntimeSnapshot.StageSnapshots
            .SelectMany(stage => stage.SubstageSnapshots)
            .FirstOrDefault(substage => substage.SubstageSnapshotId == session.ActiveSubstageId)
            ?.PlayMode == SubstagePlayMode.Trivia;
    }
}
