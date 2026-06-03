using umbral_backend.Application.Common.Security;

namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

[Authorize(Roles = "Participant")]
public sealed record ValidateParticipantMembershipAccessQuery(
    Guid LiveSessionId,
    Guid TeamId,
    string? Token) : IRequest<ParticipantMembershipAccessDecisionDto>;
