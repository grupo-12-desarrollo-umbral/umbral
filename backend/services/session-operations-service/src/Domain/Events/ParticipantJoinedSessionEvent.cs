namespace umbral_backend.Domain.Events;

public sealed class ParticipantJoinedSessionEvent : BaseEvent
{
    public ParticipantJoinedSessionEvent(Guid liveSessionId, Guid participantId, Guid teamId, DateTimeOffset occurredAt)
    {
        LiveSessionId = liveSessionId;
        ParticipantId = participantId;
        TeamId = teamId;
        OccurredAt = occurredAt;
    }

    public Guid LiveSessionId { get; }

    public Guid ParticipantId { get; }

    public Guid TeamId { get; }

    public DateTimeOffset OccurredAt { get; }
}
