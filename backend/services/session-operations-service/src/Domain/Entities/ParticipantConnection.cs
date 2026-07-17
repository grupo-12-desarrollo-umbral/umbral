using umbral_backend.Domain.Common;

namespace umbral_backend.Domain.Entities;

// A single live transport connection (a SignalR ConnectionId) held by a participant. Presence is
// decrement-guarded on this collection: a participant is marked Disconnected only once its last
// connection drops (SessionParticipant.DropConnection), so overlapping reconnect/disconnect callbacks
// across sockets — or service replicas — cannot record an actively connected participant as
// disconnected (concurrency Finding 5). Owned by SessionParticipant; the ConnectionId is the key.
public sealed class ParticipantConnection : BaseEntity
{
    private ParticipantConnection()
    {
        SessionParticipantId = Guid.Empty;
        ConnectionId = string.Empty;
    }

    private ParticipantConnection(Guid sessionParticipantId, string connectionId, DateTimeOffset openedAt)
    {
        SessionParticipantId = sessionParticipantId;
        ConnectionId = connectionId;
        OpenedAt = openedAt;
        LastSeenAt = openedAt;
    }

    public Guid SessionParticipantId { get; private set; }

    public string ConnectionId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    internal static ParticipantConnection Open(Guid sessionParticipantId, string connectionId, DateTimeOffset openedAt)
    {
        return new ParticipantConnection(sessionParticipantId, connectionId, openedAt);
    }

    internal void Touch(DateTimeOffset seenAt) => LastSeenAt = seenAt;
}
