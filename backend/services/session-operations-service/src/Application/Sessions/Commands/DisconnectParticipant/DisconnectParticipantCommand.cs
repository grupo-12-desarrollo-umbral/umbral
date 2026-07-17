using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

// Connection-oriented disconnect: dispatched from the hub on socket drop with the dropping
// Context.ConnectionId. The aggregate drops that one connection and only marks the participant
// Disconnected when it was the last (decrement-guarded), so it is safe against reconnect overlap.
[Authorize(Roles = "Participant")]
public sealed record DisconnectParticipantCommand(
    Guid LiveSessionId,
    Guid SessionParticipantId,
    string ConnectionId) : IRequest<Unit>;
