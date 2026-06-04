using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Services.SessionStates;

internal abstract class LiveSessionStateBase : ILiveSessionState
{
    public abstract SessionState State { get; }

    public abstract bool CanTransitionTo(SessionState nextState);

    public virtual void Enter(LiveSession session, DateTimeOffset occurredAt)
    {
    }

    public virtual AuthoritativeSessionTimerSnapshot GetTimerSnapshot(LiveSession session, DateTimeOffset observedAt)
    {
        return session.GetFrozenSessionTimerSnapshot(observedAt);
    }

    public virtual AuthoritativeSessionTimerSnapshot MarkTimerExpiredIfElapsed(LiveSession session, DateTimeOffset occurredAt)
    {
        return GetTimerSnapshot(session, occurredAt);
    }

    public virtual bool IsSessionTimerAdvancing(LiveSession session)
    {
        return false;
    }
}
