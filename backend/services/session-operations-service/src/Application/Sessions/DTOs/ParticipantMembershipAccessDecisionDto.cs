namespace umbral_backend.Application.Sessions.DTOs;

public sealed record ParticipantMembershipAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string Reason,
    Guid LiveSessionId,
    Guid TeamId);
