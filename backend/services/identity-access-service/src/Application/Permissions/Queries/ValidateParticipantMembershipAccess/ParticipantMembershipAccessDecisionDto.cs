namespace umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

public sealed record ParticipantMembershipAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string ReasonCode,
    string Reason,
    Guid LiveSessionId,
    Guid TeamId);
