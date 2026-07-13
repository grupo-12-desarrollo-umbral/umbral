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
        session.EnterActiveSubstageTimerState(occurredAt);
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

    public override AuthoritativeSessionTimerSnapshot GetSubstageTimerSnapshot(LiveSession session, DateTimeOffset observedAt)
    {
        return session.GetAdvancingSubstageTimerSnapshot(observedAt);
    }

    public override AuthoritativeSessionTimerSnapshot MarkSubstageTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt)
    {
        return session.MarkAdvancingSubstageTimerExpiredIfElapsed(occurredAt);
    }

    public override bool IsSubstageTimerAdvancing(LiveSession session)
    {
        return session.HasAdvancingSubstageTimer();
    }

    public override void EnsureCanAdvanceSubstage(LiveSession session)
    {
        // Active is the only state that advances substages (timer-driven).
    }

    public override void EnsureCanRegisterEvidence(LiveSession session)
    {
        // Active is the only state that admits evidence.
    }
}
