using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;

[Authorize(Roles = "Administrator,Operator")]
public sealed record AssignParticipantToTeamCommand(Guid TeamId, int UserId) : IRequest<Guid>;
