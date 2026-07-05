using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class ActiveLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Active;

    public override bool CanTransitionTo(SessionState nextState)
    {
        return nextState is SessionState.Paused or SessionState.Finished or SessionState.Cancelled;
    }

    public override void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
        session.EnterActiveSessionState(occurredAt);
        session.EnterActiveQuestionTimerState(occurredAt);
    }

    public override AuthoritativeSessionTimerSnapshot GetQuestionTimerSnapshot(LiveSession session, DateTimeOffset observedAt)
    {
        return session.GetAdvancingQuestionTimerSnapshot(observedAt);
    }

    public override AuthoritativeSessionTimerSnapshot MarkQuestionTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt)
    {
        return session.MarkAdvancingQuestionTimerExpiredIfElapsed(occurredAt);
    }

    public override bool IsQuestionTimerAdvancing(LiveSession session)
    {
        return session.HasAdvancingQuestionTimer();
    }
}
