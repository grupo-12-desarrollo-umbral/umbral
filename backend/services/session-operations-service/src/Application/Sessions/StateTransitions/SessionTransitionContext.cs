using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.StateTransitions;

public sealed class SessionTransitionContext
{
    public SessionTransitionContext(LiveSession session, SessionState targetState, string? reason)
    {
        Session = session;
        TargetState = targetState;
        Reason = reason;
    }

    public LiveSession Session { get; }

    public SessionState TargetState { get; }

    public string? Reason { get; }
}
