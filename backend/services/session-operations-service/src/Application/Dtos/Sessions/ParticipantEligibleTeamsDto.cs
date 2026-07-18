namespace umbral_backend.Application.Dtos.Sessions;

// Mirror of users-service GET /api/permissions/participant-eligible-teams (#107). Session-independent
// whitelist of RegisteredTeams the current participant may join. TeamId is the *reference* (catalog) team
// id — the lobby read (#108) and self-join write (#110) intersect it with a LiveSession's per-run
// Team.ReferenceTeamId. IsEligible=false with an empty Teams list is a denied/deactivated user; IsEligible=true
// with an empty list is an unassigned participant → Open Team Selection (all attached teams selectable).
public sealed record ParticipantEligibleTeamsDto(
    bool IsEligible,
    string ReasonCode,
    IReadOnlyList<EligibleTeamDto> Teams);

public sealed record EligibleTeamDto(
    Guid TeamId,
    string DisplayName,
    string TeamCode);
