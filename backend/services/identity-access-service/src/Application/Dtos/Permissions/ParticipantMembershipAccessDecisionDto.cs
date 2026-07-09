namespace umbral_backend.Application.Dtos.Permissions;

public sealed record ParticipantMembershipAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string ReasonCode,
    string Reason,
    Guid LiveSessionId,
    Guid TeamId);
