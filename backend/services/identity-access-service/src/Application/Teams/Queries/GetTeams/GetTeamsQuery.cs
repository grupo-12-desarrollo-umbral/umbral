using umbral_backend.Application.Common.Models;
using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Teams.Common;

namespace umbral_backend.Application.Teams.Queries.GetTeams;

[Authorize(Roles = "Administrator,Operator")]
public sealed record GetTeamsQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<TeamDto>>;
