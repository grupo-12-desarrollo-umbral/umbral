using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Teams;

namespace umbral_backend.Application.Teams.Queries.GetTeamById;

[Authorize(Roles = "Administrator,Operator")]
public sealed record GetTeamByIdQuery(Guid TeamId) : IRequest<TeamDto>;
