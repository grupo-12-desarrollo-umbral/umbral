using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public sealed class JoinTokenPolicy
{
    public void EnsureCanIssue(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        if (expiresAt <= issuedAt)
        {
            throw new JoinTokenExpirationInvalidException();
        }
    }

    public void EnsureCanConsume(JoinToken joinToken, DateTimeOffset consumedAt)
    {
        EnsureIsValid(joinToken, consumedAt);
    }

    public void EnsureIsValid(JoinToken joinToken, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(joinToken);

        if (joinToken.Status == JoinTokenStatus.Consumed || joinToken.ConsumedAt.HasValue)
        {
            throw new JoinTokenReplayRejectedException(joinToken.JoinTokenId, JoinTokenStatus.Consumed);
        }

        if (joinToken.Status == JoinTokenStatus.Revoked)
        {
            throw new JoinTokenReplayRejectedException(joinToken.JoinTokenId, JoinTokenStatus.Revoked);
        }

        if (joinToken.Status == JoinTokenStatus.Expired || now > joinToken.ExpiresAt)
        {
            throw new JoinTokenExpiredException(joinToken.JoinTokenId, joinToken.ExpiresAt, now);
        }

        if (joinToken.Status != JoinTokenStatus.Active)
        {
            throw new JoinTokenReplayRejectedException(joinToken.JoinTokenId, joinToken.Status);
        }
    }
}
