namespace umbral_backend.Application.Dtos.Sessions;

public sealed record ParticipantMembershipAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string ReasonCode,
    string Reason,
    Guid LiveSessionId,
    Guid TeamId);
