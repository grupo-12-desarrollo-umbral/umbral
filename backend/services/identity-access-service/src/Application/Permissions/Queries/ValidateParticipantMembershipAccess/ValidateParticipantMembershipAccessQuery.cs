using umbral_backend.Application.Dtos.Permissions;

namespace umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

public sealed record ValidateParticipantMembershipAccessQuery(
    Guid LiveSessionId,
    Guid TeamId) : IRequest<ParticipantMembershipAccessDecisionDto>;
