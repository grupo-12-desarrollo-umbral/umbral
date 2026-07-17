using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

public sealed class SessionParticipant : BaseEntity
{
    private readonly List<ParticipantConnection> _connections = new();

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

    // The live transport connections (SignalR ConnectionIds) currently held by this participant.
    // Presence decrements against this collection — see DropConnection.
    public IReadOnlyCollection<ParticipantConnection> Connections => _connections.AsReadOnly();

    public int ActiveConnectionCount => _connections.Count;

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

    // Hard disconnect: drops every held connection and marks the participant Disconnected regardless of
    // how many sockets were open. Used by operator/seed paths that force a participant offline; the
    // per-connection socket lifecycle goes through RegisterConnection / DropConnection instead.
    public void Disconnect(DateTimeOffset seenAt)
    {
        if (IsRemoved)
        {
            throw new ParticipantRemovedFromSessionException(SessionParticipantId);
        }

        _connections.Clear();
        ParticipantStatus = ParticipantStatus.Disconnected;
        LastSeenAt = seenAt;
    }

    // Registers a live transport connection under this participant and refreshes presence to Active.
    // Idempotent: re-registering the same ConnectionId only touches its heartbeat, so a retried
    // admission (e.g. after an xmin conflict) never double-counts a socket. A Removed participant
    // cannot re-open a connection.
    public void RegisterConnection(string connectionId, DateTimeOffset seenAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        if (IsRemoved)
        {
            throw new ParticipantRemovedFromSessionException(SessionParticipantId);
        }

        var existing = _connections.SingleOrDefault(connection => connection.ConnectionId == connectionId);
        if (existing is null)
        {
            _connections.Add(ParticipantConnection.Open(SessionParticipantId, connectionId, seenAt));
        }
        else
        {
            existing.Touch(seenAt);
        }

        ParticipantStatus = ParticipantStatus.Active;
        LastSeenAt = seenAt;
    }

    // Drops a single transport connection, keyed on ConnectionId. Idempotent — dropping a ConnectionId
    // that is not (or no longer) registered is a no-op, so a duplicate disconnect callback changes
    // nothing. Decrement-guarded — the participant is marked Disconnected only when its LAST connection
    // leaves, so an old socket's disconnect that overlaps a fresh connection on another socket (or a
    // reconnect that already registered its own ConnectionId) never records the participant as
    // disconnected (Finding 5). Returns true only on the transition to Disconnected.
    public bool DropConnection(string connectionId, DateTimeOffset seenAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        var existing = _connections.SingleOrDefault(connection => connection.ConnectionId == connectionId);
        if (existing is null)
        {
            return false;
        }

        _connections.Remove(existing);

        if (_connections.Count > 0 || IsRemoved || IsDisconnected)
        {
            return false;
        }

        ParticipantStatus = ParticipantStatus.Disconnected;
        LastSeenAt = seenAt;
        return true;
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
