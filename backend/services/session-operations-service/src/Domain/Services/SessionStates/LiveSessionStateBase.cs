using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services.SessionStates;

internal abstract class LiveSessionStateBase : ILiveSessionState
{
    public abstract SessionState State { get; }

    public abstract bool CanTransitionTo(SessionState nextState);

    public virtual void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
    }

    public virtual AuthoritativeSessionTimerSnapshot GetQuestionTimerSnapshot(LiveSession session, DateTimeOffset observedAt)
    {
        return session.GetFrozenQuestionTimerSnapshot(observedAt);
    }

    public virtual AuthoritativeSessionTimerSnapshot MarkQuestionTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt)
    {
        return GetQuestionTimerSnapshot(session, occurredAt);
    }

    public virtual bool IsQuestionTimerAdvancing(LiveSession session)
    {
        return false;
    }

    public virtual void EnsureCanAdvanceSubstage(LiveSession session)
    {
        throw new SubstageAdvancementRequiresActiveSessionException(session.State);
    }

    public virtual void EnsureCanRegisterTriviaAnswer(LiveSession session)
    {
        throw new TriviaAnswerRequiresActiveSessionException(session.State);
    }
}
