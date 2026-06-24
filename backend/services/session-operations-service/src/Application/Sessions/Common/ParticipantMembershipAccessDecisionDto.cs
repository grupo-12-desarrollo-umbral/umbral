namespace umbral_backend.Application.Sessions.Common;

public sealed record ParticipantMembershipAccessDecisionDto(
    string Capability,
    bool IsAllowed,
    string Reason,
    Guid LiveSessionId,
    Guid TeamId);
