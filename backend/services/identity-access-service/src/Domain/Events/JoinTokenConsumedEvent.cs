namespace umbral_backend.Domain.Events;

public sealed class JoinTokenConsumedEvent : BaseEvent
{
    public JoinTokenConsumedEvent(Guid joinTokenId, Guid liveSessionId, Guid teamId, DateTimeOffset consumedAt)
    {
        JoinTokenId = joinTokenId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        ConsumedAt = consumedAt;
    }

    public Guid JoinTokenId { get; }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public DateTimeOffset ConsumedAt { get; }
}
