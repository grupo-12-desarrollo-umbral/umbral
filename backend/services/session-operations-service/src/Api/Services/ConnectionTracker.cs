using System.Diagnostics.CodeAnalysis;

namespace umbral_backend.Api.Services;

public sealed class ConnectionTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, ConnectionParticipant> _connections = new();
    private readonly Dictionary<Guid, int> _participantConnectionCounts = new();

    public void Add(string connectionId, Guid liveSessionId, Guid sessionParticipantId)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out var previousParticipant))
            {
                DecrementConnectionCount(previousParticipant.SessionParticipantId);
            }

            _connections[connectionId] = new ConnectionParticipant(liveSessionId, sessionParticipantId);
            IncrementConnectionCount(sessionParticipantId);
        }
    }

    public bool TryRemove(
        string connectionId,
        [NotNullWhen(true)] out ConnectionParticipant? participant,
        out bool hasRemainingConnections)
    {
        lock (_gate)
        {
            if (!_connections.Remove(connectionId, out participant))
            {
                hasRemainingConnections = false;
                return false;
            }

            hasRemainingConnections = DecrementConnectionCount(participant.SessionParticipantId) > 0;
            return true;
        }
    }

    private void IncrementConnectionCount(Guid sessionParticipantId)
    {
        _participantConnectionCounts.TryGetValue(sessionParticipantId, out var currentCount);
        _participantConnectionCounts[sessionParticipantId] = currentCount + 1;
    }

    private int DecrementConnectionCount(Guid sessionParticipantId)
    {
        if (!_participantConnectionCounts.TryGetValue(sessionParticipantId, out var currentCount) || currentCount <= 1)
        {
            _participantConnectionCounts.Remove(sessionParticipantId);
            return 0;
        }

        var remainingCount = currentCount - 1;
        _participantConnectionCounts[sessionParticipantId] = remainingCount;
        return remainingCount;
    }

    public sealed record ConnectionParticipant(Guid LiveSessionId, Guid SessionParticipantId);
}
