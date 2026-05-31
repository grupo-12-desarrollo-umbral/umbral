using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.DeactivateTeam;

[Authorize(Roles = "Administrator")]
public sealed record DeactivateTeamCommand(Guid TeamId) : IRequest;
