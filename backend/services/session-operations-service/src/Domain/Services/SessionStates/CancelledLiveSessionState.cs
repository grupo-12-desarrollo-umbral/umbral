using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class CancelledLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Cancelled;

    public override bool CanTransitionTo(SessionState nextState)
    {
        return false;
    }

    public override void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
        session.EnterCancelledSessionState(occurredAt);
    }
}
