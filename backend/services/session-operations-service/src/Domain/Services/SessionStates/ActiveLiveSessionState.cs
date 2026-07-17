using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services.SessionStates;

internal sealed class ActiveLiveSessionState : LiveSessionStateBase
{
    public override SessionState State => SessionState.Active;

    public override bool CanTransitionTo(SessionState nextState)
    {
        // Finished is deliberately excluded: it is reached only via SessionCompletion
        // (LiveSession.CompleteActiveSubstageAndAdvance applies it directly, bypassing this policy
        // gate), never via manual Operator transition.
        return nextState is SessionState.Paused or SessionState.Cancelled;
    }

    public override void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
        session.EnterActiveSessionState(occurredAt);
        session.EnterActiveQuestionTimerState(occurredAt);
        session.EnterActiveMissionTimerState(occurredAt);
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

    public override AuthoritativeSessionTimerSnapshot GetMissionTimerSnapshot(LiveSession session, DateTimeOffset observedAt)
    {
        return session.GetAdvancingMissionTimerSnapshot(observedAt);
    }

    public override AuthoritativeSessionTimerSnapshot MarkMissionTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt)
    {
        return session.MarkAdvancingMissionTimerExpiredIfElapsed(occurredAt);
    }

    public override bool IsMissionTimerAdvancing(LiveSession session)
    {
        return session.HasAdvancingMissionTimer();
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
