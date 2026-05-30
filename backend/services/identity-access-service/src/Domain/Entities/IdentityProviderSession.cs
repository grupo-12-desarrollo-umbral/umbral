using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class IdentityProviderSession : BaseAuditableEntity
{
    private IdentityProviderSession()
    {
        ProviderName = string.Empty;
        ProviderSessionKey = string.Empty;
    }

    private IdentityProviderSession(
        int userId,
        string providerName,
        string providerSessionKey,
        DateTimeOffset startedAt,
        DateTimeOffset expiresAt)
    {
        if (userId <= 0)
        {
            throw new IdentityProviderSessionUserRequiredException();
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new IdentityProviderNameRequiredException();
        }

        if (string.IsNullOrWhiteSpace(providerSessionKey))
        {
            throw new IdentityProviderSessionKeyRequiredException();
        }

        if (expiresAt <= startedAt)
        {
            throw new IdentityProviderSessionExpirationInvalidException();
        }

        UserId = userId;
        ProviderName = providerName.Trim();
        ProviderSessionKey = providerSessionKey.Trim();
        StartedAt = startedAt;
        ExpiresAt = expiresAt;
    }

    public int UserId { get; private set; }

    public string ProviderName { get; private set; }

    public string ProviderSessionKey { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public static IdentityProviderSession Start(
        int userId,
        string providerName,
        string providerSessionKey,
        DateTimeOffset startedAt,
        DateTimeOffset expiresAt)
    {
        var session = new IdentityProviderSession(userId, providerName, providerSessionKey, startedAt, expiresAt);
        session.AddDomainEvent(new IdentityProviderSessionStartedEvent(session));

        return session;
    }

    public void End(DateTimeOffset endedAt)
    {
        if (RevokedAt.HasValue)
        {
            return;
        }

        RevokedAt = endedAt;
        AddDomainEvent(new IdentityProviderSessionEndedEvent(this));
    }
}
