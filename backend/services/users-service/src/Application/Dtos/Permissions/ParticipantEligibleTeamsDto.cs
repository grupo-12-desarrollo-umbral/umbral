namespace umbral_backend.Application.Dtos.Permissions;

// Session-independent set of RegisteredTeams the current participant is generally eligible for.
// SessionOperations intersects this with a LiveSession's teams (issue #90 query split); the
// per-join point check stays the authoritative gate (issue #105 follow-up to #104).
public sealed record ParticipantEligibleTeamsDto(
    bool IsEligible,
    string ReasonCode,
    IReadOnlyList<EligibleTeamDto> Teams);

public sealed record EligibleTeamDto(
    Guid TeamId,
    string DisplayName,
    string TeamCode);
