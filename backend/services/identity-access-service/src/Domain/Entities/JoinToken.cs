using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Domain.Entities;

public sealed class JoinToken : BaseAuditableEntity
{
    private JoinToken()
    {
        JoinTokenId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
        TokenHash = string.Empty;
        Status = JoinTokenStatus.Active;
    }

    private JoinToken(
        Guid joinTokenId,
        Guid liveSessionId,
        Guid teamId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int issuedByUserId,
        JoinTokenPolicy policy)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new JoinTokenLiveSessionRequiredException();
        }

        if (teamId == Guid.Empty)
        {
            throw new JoinTokenTeamRequiredException();
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new JoinTokenHashRequiredException();
        }

        if (issuedByUserId <= 0)
        {
            throw new JoinTokenIssuerRequiredException();
        }

        ArgumentNullException.ThrowIfNull(policy);

        issuedAt = TruncateToMicroseconds(issuedAt);
        expiresAt = TruncateToMicroseconds(expiresAt);
        policy.EnsureCanIssue(issuedAt, expiresAt);

        JoinTokenId = joinTokenId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        TokenHash = tokenHash.Trim();
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        IssuedByUserId = issuedByUserId;
        Status = JoinTokenStatus.Active;
    }

    public Guid JoinTokenId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ConsumedAt { get; private set; }

    public int IssuedByUserId { get; private set; }

    public JoinTokenStatus Status { get; private set; }

    public static JoinToken Issue(
        Guid liveSessionId,
        Guid teamId,
        string tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        int issuedByUserId,
        JoinTokenPolicy policy)
    {
        var joinToken = new JoinToken(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            tokenHash,
            issuedAt,
            expiresAt,
            issuedByUserId,
            policy);

        joinToken.AddDomainEvent(new JoinTokenIssuedEvent(
            joinToken.JoinTokenId,
            joinToken.LiveSessionId,
            joinToken.TeamId,
            joinToken.IssuedAt,
            joinToken.ExpiresAt,
            joinToken.IssuedByUserId));

        return joinToken;
    }

    public void Consume(DateTimeOffset consumedAt, JoinTokenPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        consumedAt = TruncateToMicroseconds(consumedAt);
        policy.EnsureCanConsume(this, consumedAt);

        ConsumedAt = consumedAt;
        Status = JoinTokenStatus.Consumed;
        AddDomainEvent(new JoinTokenConsumedEvent(JoinTokenId, LiveSessionId, TeamId, consumedAt));
    }

    // PostgreSQL timestamp/timestamptz stores microsecond (6-digit) precision, while
    // DateTimeOffset carries 100-nanosecond (7-digit) ticks. Normalising at the domain
    // boundary keeps the in-memory model, persisted rows, and emitted domain events in
    // exact agreement instead of drifting by a truncated 7th fractional digit.
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
        => new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
}
