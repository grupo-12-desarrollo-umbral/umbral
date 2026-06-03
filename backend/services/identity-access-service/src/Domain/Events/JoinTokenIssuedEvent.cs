namespace umbral_backend.Domain.Events;

public sealed class JoinTokenIssuedEvent : BaseEvent
{
    public JoinTokenIssuedEvent(
        Guid joinTokenId,
        Guid liveSessionId,
        Guid teamId,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int issuedByUserId)
    {
        JoinTokenId = joinTokenId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        IssuedByUserId = issuedByUserId;
    }

    public Guid JoinTokenId { get; }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public int IssuedByUserId { get; }
}
