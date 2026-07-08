using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class SessionParticipant : BaseEntity
{
    private SessionParticipant()
    {
        SessionParticipantId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        ExternalIdentityId = Guid.Empty;
        DisplayName = string.Empty;
    }

    private SessionParticipant(Guid liveSessionId, Guid externalIdentityId, string displayName, DateTimeOffset joinedAt)
    {
        if (externalIdentityId == Guid.Empty)
        {
            throw new ParticipantIdentityRequiredException();
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ParticipantDisplayNameRequiredException();
        }

        SessionParticipantId = Guid.NewGuid();
        LiveSessionId = liveSessionId;
        ExternalIdentityId = externalIdentityId;
        DisplayName = displayName.Trim();
        ParticipantStatus = ParticipantStatus.Joined;
        JoinedAt = joinedAt;
        LastSeenAt = joinedAt;
    }

    public Guid SessionParticipantId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid ExternalIdentityId { get; private set; }

    public string DisplayName { get; private set; }

    public ParticipantStatus ParticipantStatus { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public bool IsDisconnected => ParticipantStatus == ParticipantStatus.Disconnected;

    public bool IsRemoved => ParticipantStatus == ParticipantStatus.Removed;

    public bool IsBlocked => ParticipantStatus == ParticipantStatus.Blocked;

    public static SessionParticipant Join(Guid liveSessionId, Guid externalIdentityId, string displayName, DateTimeOffset joinedAt)
    {
        return new SessionParticipant(liveSessionId, externalIdentityId, displayName, joinedAt);
    }

    public void MarkActive(DateTimeOffset seenAt)
    {
        if (IsRemoved)
        {
            throw new ParticipantRemovedFromSessionException(SessionParticipantId);
        }

        if (ParticipantStatus == ParticipantStatus.Active)
        {
            throw new ParticipantAlreadyConnectedException(SessionParticipantId);
        }

        ParticipantStatus = ParticipantStatus.Active;
        LastSeenAt = seenAt;
    }

    public void Disconnect(DateTimeOffset seenAt)
    {
        if (IsRemoved)
        {
            throw new ParticipantRemovedFromSessionException(SessionParticipantId);
        }

        ParticipantStatus = ParticipantStatus.Disconnected;
        LastSeenAt = seenAt;
    }

    public void Remove(DateTimeOffset occurredAt)
    {
        ParticipantStatus = ParticipantStatus.Removed;
        LastSeenAt = occurredAt;
    }

    // Participation Block (#91). Idempotent: returns true only on the transition, so a repeated
    // denied re-check does not re-persist/re-notify. Removed stays terminal.
    public bool Block(DateTimeOffset occurredAt)
    {
        if (IsRemoved || IsBlocked)
        {
            return false;
        }

        ParticipantStatus = ParticipantStatus.Blocked;
        LastSeenAt = occurredAt;
        return true;
    }

    public void RefreshPresence(DateTimeOffset seenAt)
    {
        if (IsRemoved)
        {
            throw new ParticipantRemovedFromSessionException(SessionParticipantId);
        }

        ParticipantStatus = ParticipantStatus.Active;
        LastSeenAt = seenAt;
    }
}
