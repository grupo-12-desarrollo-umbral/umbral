using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.StateTransitions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Commands.TransitionSessionState;

public sealed class TransitionSessionStateCommandHandler
    : IRequestHandler<TransitionSessionStateCommand, TransitionSessionStateResultDto>
{
    private readonly ISessionAdministrationAccessResolver _sessionAdministrationAccessResolver;
    private readonly SessionTransitionChain _validatorChain;
    private readonly SessionStateTransitionPolicy _transitionPolicy;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly TimeProvider _timeProvider;

    public TransitionSessionStateCommandHandler(
        ISessionAdministrationAccessResolver sessionAdministrationAccessResolver,
        SessionTransitionChain validatorChain,
        SessionStateTransitionPolicy transitionPolicy,
        ILiveSessionRepository liveSessionRepository,
        TimeProvider timeProvider)
    {
        _sessionAdministrationAccessResolver = sessionAdministrationAccessResolver;
        _validatorChain = validatorChain;
        _transitionPolicy = transitionPolicy;
        _liveSessionRepository = liveSessionRepository;
        _timeProvider = timeProvider;
    }

    public async Task<TransitionSessionStateResultDto> Handle(
        TransitionSessionStateCommand request,
        CancellationToken cancellationToken)
    {
        var authorizedAccess = await _sessionAdministrationAccessResolver.GetAuthorizedSessionWithActorAsync(
            request.LiveSessionId,
            cancellationToken);
        var liveSession = authorizedAccess.Session;

        var context = new SessionTransitionContext(
            liveSession,
            request.TargetState,
            request.Reason,
            authorizedAccess.ResponsibleUserId);
        await _validatorChain.ValidateAsync(context, cancellationToken);

        var previousState = liveSession.State;

        // The domain transition re-asserts its own invariant as the last line of defence and raises
        // SessionStateChangedEvent. The dispatch interceptor captures its integration publications
        // in the bus outbox pre-commit and fans out its SignalR notification post-commit.
        var occurredAt = _timeProvider.GetUtcNow();
        liveSession.MoveTo(
            context.TargetState,
            occurredAt,
            _transitionPolicy,
            context.Reason,
            context.ResponsibleUserId);
        var timerSnapshot = liveSession.GetAuthoritativeSessionTimerSnapshot(occurredAt);

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new TransitionSessionStateResultDto(
            liveSession.LiveSessionId,
            previousState.ToString(),
            liveSession.State.ToString(),
            liveSession.LastStateChangedAt,
            SessionTimerSnapshotDtoFactory.Create(liveSession, teamId: null, timerSnapshot));
    }
}
