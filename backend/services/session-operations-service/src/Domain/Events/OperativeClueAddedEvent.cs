namespace umbral_backend.Domain.Events;

public sealed class OperativeClueAddedEvent : BaseEvent
{
    public OperativeClueAddedEvent(
        Guid operativeClueId,
        Guid liveSessionId,
        Guid teamId,
        string clueText,
        int createdByUserId,
        DateTimeOffset createdAt)
    {
        OperativeClueId = operativeClueId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        ClueText = clueText;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public Guid OperativeClueId { get; }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public string ClueText { get; }

    public int CreatedByUserId { get; }

    public DateTimeOffset CreatedAt { get; }
}
