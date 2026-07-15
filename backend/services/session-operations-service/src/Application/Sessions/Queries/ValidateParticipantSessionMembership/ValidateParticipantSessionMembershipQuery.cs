using umbral_backend.Application.Common.Security;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Application.Sessions.Queries.ValidateParticipantSessionMembership;

[Authorize(Roles = "Participant")]
public sealed record ValidateParticipantSessionMembershipQuery(
    Guid LiveSessionId,
    Guid TeamId) : IRequest<ParticipantSessionMembershipDto>;
