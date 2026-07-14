namespace umbral_backend.Application.Dtos.Sessions;

public sealed record OperatorSessionPanelDto(
    Guid LiveSessionId,
    string State,
    SessionTimerSnapshotDto Timer,
    IReadOnlyList<OperatorTeamProgressDto> TeamProgress);

public sealed record OperatorTeamProgressDto(
    Guid TeamId,
    string TeamCode,
    string DisplayName,
    int Score,
    // Clues visible to this team: manual operator releases + the active substage's initial clues.
    int ReleasedClueCount,
    ActiveSubstageContextDto? ActiveSubstage);
