using umbral_backend.Application.Dtos.Teams;
using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Teams.Queries.GetTeamParticipants;

[Authorize(Roles = "Administrator,Operator")]
public sealed record GetTeamParticipantsQuery(Guid TeamId) : IRequest<IReadOnlyList<RegisteredTeamMembershipDto>>;
