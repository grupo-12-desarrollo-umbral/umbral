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
    ActiveSubstageContextDto? ActiveSubstage);
