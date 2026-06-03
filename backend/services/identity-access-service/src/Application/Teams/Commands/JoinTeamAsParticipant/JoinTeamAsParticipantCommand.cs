using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.JoinTeamAsParticipant;

[Authorize(Roles = "Participant")]
public sealed record JoinTeamAsParticipantCommand(Guid LiveSessionId, Guid TeamId) : IRequest<Guid>;
