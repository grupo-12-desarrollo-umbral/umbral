using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class ScheduledLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Scheduled;

    public override bool CanTransitionTo(SessionState nextState)
    {
        return nextState is SessionState.Preparing or SessionState.Cancelled;
    }
}
