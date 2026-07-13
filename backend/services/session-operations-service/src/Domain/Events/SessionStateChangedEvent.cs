using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

public sealed class SessionStateChangedEvent : BaseEvent
{
    public SessionStateChangedEvent(
        Guid liveSessionId,
        SessionState previousState,
        SessionState currentState,
        DateTimeOffset changedAt,
        int? responsibleUserId = null,
        string? reason = null,
        SessionEventActorType actorType = SessionEventActorType.System)
    {
        LiveSessionId = liveSessionId;
        PreviousState = previousState;
        CurrentState = currentState;
        ChangedAt = changedAt;
        ResponsibleUserId = responsibleUserId;
        Reason = reason;
        ActorType = actorType;
    }

    public Guid LiveSessionId { get; }

    public SessionState PreviousState { get; }

    public SessionState CurrentState { get; }

    public DateTimeOffset ChangedAt { get; }

    public int? ResponsibleUserId { get; }

    public string? Reason { get; }

    public SessionEventActorType ActorType { get; }
}
