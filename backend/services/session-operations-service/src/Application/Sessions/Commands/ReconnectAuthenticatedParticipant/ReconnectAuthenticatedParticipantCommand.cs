using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

// ConnectionId is the SignalR Context.ConnectionId when the reconnect arrives over the hub; the
// aggregate registers it as a decrement-guarded presence lease. The HTTP reconnect endpoint carries
// no socket, so it passes null and only refreshes presence without opening a lease.
[Authorize(Roles = "Participant")]
public sealed record ReconnectAuthenticatedParticipantCommand(
    Guid LiveSessionId,
    Guid TeamId,
    string DisplayName,
    string? Token,
    string? ConnectionId = null) : IRequest<ReconnectParticipantResultDto>;
