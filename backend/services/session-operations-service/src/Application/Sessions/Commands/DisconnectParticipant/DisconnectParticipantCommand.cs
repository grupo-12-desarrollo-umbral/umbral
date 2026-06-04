using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

[Authorize(Roles = "Participant")]
public sealed record DisconnectParticipantCommand(
    Guid LiveSessionId,
    Guid SessionParticipantId) : IRequest<Unit>;
