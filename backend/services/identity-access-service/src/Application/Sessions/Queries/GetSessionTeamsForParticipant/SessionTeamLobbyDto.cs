namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamsForParticipant;

public sealed record SessionTeamLobbyDto(
    Guid LiveSessionId,
    string SessionCode,
    IReadOnlyList<SessionTeamLobbyTeamDto> Teams);

public sealed record SessionTeamLobbyTeamDto(
    Guid TeamId,
    string DisplayName,
    string JoinState);

public sealed record SessionTeamLobbyEntry(
    Guid TeamId,
    string DisplayName,
    bool IsCallerMember);
