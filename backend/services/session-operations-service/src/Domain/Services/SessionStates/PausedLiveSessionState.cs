using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class PausedLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Paused;

    public override bool CanTransitionTo(SessionState nextState)
    {
        // Finished is deliberately excluded: it is reached only via SessionCompletion, which fires
        // only while Active (substage completion is timer-driven), never via manual Operator
        // transition from Paused.
        return nextState is SessionState.Active or SessionState.Cancelled;
    }

    public override void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
        session.EnterPausedSessionState(occurredAt);
        session.EnterPausedQuestionTimerState(occurredAt);
        session.EnterPausedMissionTimerState(occurredAt);
    }
}
