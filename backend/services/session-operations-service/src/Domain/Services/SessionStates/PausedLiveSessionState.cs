using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class PausedLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Paused;

    public override bool CanTransitionTo(SessionState nextState)
    {
        return nextState is SessionState.Active or SessionState.Finished or SessionState.Cancelled;
    }

    public override void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
        session.EnterPausedSessionState(occurredAt);
    }
}
