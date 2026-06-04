using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class PreparingLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Preparing;

    public override bool CanTransitionTo(SessionState nextState)
    {
        return nextState is SessionState.Active or SessionState.Cancelled;
    }
}
