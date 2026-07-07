using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.AuthorizeParticipantForTeam;

[Authorize(Roles = "Administrator,Operator")]
public sealed record AuthorizeParticipantForTeamCommand(Guid TeamId, int UserId) : IRequest<Guid>;
