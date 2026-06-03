using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

[Authorize(Roles = "Participant")]
public sealed record ReconnectAuthenticatedParticipantCommand(
    Guid LiveSessionId,
    Guid TeamId,
    string DisplayName,
    string? Token) : IRequest<ReconnectParticipantResultDto>;
