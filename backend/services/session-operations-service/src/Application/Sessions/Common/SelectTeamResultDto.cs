namespace umbral_backend.Application.Sessions.Common;

// Participant self-join result (#110). The mobile client keys off TeamMembershipId; the rest is context.
public sealed record SelectTeamResultDto(
    Guid TeamMembershipId,
    Guid LiveSessionId,
    Guid TeamId,
    Guid SessionParticipantId,
    string State);
