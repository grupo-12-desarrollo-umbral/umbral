using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

[Authorize(Roles = "Participant")]
public sealed record ValidateParticipantMembershipAccessQuery(
    Guid LiveSessionId,
    Guid TeamId) : IRequest<ParticipantMembershipAccessDecisionDto>;
