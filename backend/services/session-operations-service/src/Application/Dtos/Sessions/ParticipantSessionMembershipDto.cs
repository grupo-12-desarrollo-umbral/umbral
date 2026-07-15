namespace umbral_backend.Application.Dtos.Sessions;

public sealed record ParticipantSessionMembershipDto(
    bool IsAllowed,
    Guid LiveSessionId,
    Guid TeamId,
    string ReasonCode);
