using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.UpdateTeam;

[Authorize(Roles = "Administrator")]
public sealed record UpdateTeamCommand(Guid TeamId, string DisplayName, string TeamCode) : IRequest;
