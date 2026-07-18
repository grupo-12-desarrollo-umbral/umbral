using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.DeactivateTeam;

[Authorize(Roles = "Administrator,Operator")]
public sealed record DeactivateTeamCommand(Guid TeamId) : IRequest;
