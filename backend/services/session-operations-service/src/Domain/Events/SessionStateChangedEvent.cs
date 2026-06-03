using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

public sealed class SessionStateChangedEvent : BaseEvent
{
    public SessionStateChangedEvent(Guid liveSessionId, SessionState previousState, SessionState currentState, DateTimeOffset changedAt)
    {
        LiveSessionId = liveSessionId;
        PreviousState = previousState;
        CurrentState = currentState;
        ChangedAt = changedAt;
    }

    public Guid LiveSessionId { get; }

    public SessionState PreviousState { get; }

    public SessionState CurrentState { get; }

    public DateTimeOffset ChangedAt { get; }
}
