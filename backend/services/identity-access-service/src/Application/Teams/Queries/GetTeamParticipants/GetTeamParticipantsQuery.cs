using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Teams.DTOs;

namespace umbral_backend.Application.Teams.Queries.GetTeamParticipants;

[Authorize(Roles = "Administrator,Operator")]
public sealed record GetTeamParticipantsQuery(Guid TeamId) : IRequest<IReadOnlyList<TeamMembershipDto>>;
