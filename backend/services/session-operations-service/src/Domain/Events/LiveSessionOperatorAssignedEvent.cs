namespace umbral_backend.Domain.Events;

public sealed class LiveSessionOperatorAssignedEvent : BaseEvent
{
    public LiveSessionOperatorAssignedEvent(
        Guid liveSessionId,
        int? previousOperatorUserId,
        int? assignedOperatorUserId,
        DateTimeOffset occurredAt)
    {
        LiveSessionId = liveSessionId;
        PreviousOperatorUserId = previousOperatorUserId;
        AssignedOperatorUserId = assignedOperatorUserId;
        OccurredAt = occurredAt;
    }

    public Guid LiveSessionId { get; }

    public int? PreviousOperatorUserId { get; }

    public int? AssignedOperatorUserId { get; }

    public DateTimeOffset OccurredAt { get; }
}
