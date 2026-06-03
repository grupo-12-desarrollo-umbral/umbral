namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public sealed record ParticipantMembershipAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string Reason,
    Guid LiveSessionId,
    Guid TeamId);
