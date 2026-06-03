using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class JoinContext : BaseEntity
{
    private JoinContext()
    {
        JoinContextId = Guid.Empty;
        LiveSessionId = Guid.Empty;
    }

    private JoinContext(Guid liveSessionId, Guid? teamId, Guid? joinTokenId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (expiresAt <= createdAt)
        {
            throw new JoinContextExpirationInvalidException();
        }

        JoinContextId = Guid.NewGuid();
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        JoinTokenId = joinTokenId;
        Status = JoinContextStatus.Pending;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid JoinContextId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid? TeamId { get; private set; }

    public Guid? JoinTokenId { get; private set; }

    public JoinContextStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public static JoinContext Create(Guid liveSessionId, Guid? teamId, Guid? joinTokenId, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        return new JoinContext(liveSessionId, teamId, joinTokenId, createdAt, expiresAt);
    }

    public void Consume(DateTimeOffset consumedAt)
    {
        EnsureOpen();

        if (consumedAt > ExpiresAt)
        {
            Status = JoinContextStatus.Expired;
            throw new JoinContextAlreadyClosedException(JoinContextId);
        }

        Status = JoinContextStatus.Consumed;
        ConsumedAt = consumedAt;
    }

    public void Cancel()
    {
        EnsureOpen();
        Status = JoinContextStatus.Cancelled;
    }

    public void Expire(DateTimeOffset occurredAt)
    {
        if (Status != JoinContextStatus.Pending)
        {
            throw new JoinContextAlreadyClosedException(JoinContextId);
        }

        if (occurredAt < ExpiresAt)
        {
            throw new JoinContextExpirationInvalidException();
        }

        Status = JoinContextStatus.Expired;
    }

    private void EnsureOpen()
    {
        if (Status != JoinContextStatus.Pending)
        {
            throw new JoinContextAlreadyClosedException(JoinContextId);
        }
    }
}
