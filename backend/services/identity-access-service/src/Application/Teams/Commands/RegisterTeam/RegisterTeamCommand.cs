using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Commands.RegisterTeam;

[Authorize(Roles = "Administrator,Operator")]
public sealed record RegisterTeamCommand(string DisplayName, string TeamCode) : IRequest<Guid>;
