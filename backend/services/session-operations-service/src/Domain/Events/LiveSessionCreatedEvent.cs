namespace umbral_backend.Domain.Events;

public sealed class LiveSessionCreatedEvent : BaseEvent
{
    public LiveSessionCreatedEvent(Guid liveSessionId, string sessionCode, DateTimeOffset occurredAt)
    {
        LiveSessionId = liveSessionId;
        SessionCode = sessionCode;
        OccurredAt = occurredAt;
    }

    public Guid LiveSessionId { get; }

    public string SessionCode { get; }

    public DateTimeOffset OccurredAt { get; }
}
