namespace umbral_backend.Application.Dtos.Sessions;

// Participant team lobby read (#108). The attached teams of a live session, each tagged with the
// current participant's join state: "mine" (their current pick), "joinable" (in their Open Team
// Selection set with a free slot, pre-start), or "locked". TeamId is the RUNTIME Team.TeamId — the
// id the self-join write (#110) targets, not the reference/catalog id.
public sealed record SessionTeamLobbyDto(
    Guid LiveSessionId,
    string SessionCode,
    IReadOnlyList<SessionTeamDto> Teams);

public sealed record SessionTeamDto(
    Guid TeamId,
    string DisplayName,
    string JoinState);
