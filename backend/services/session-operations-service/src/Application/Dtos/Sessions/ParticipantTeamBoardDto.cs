namespace umbral_backend.Application.Dtos.Sessions;

public sealed record ParticipantTeamBoardDto(
    Guid LiveSessionId,
    Guid TeamId,
    string TeamDisplayName,
    string TeamCode,
    int CurrentScore,
    SessionTimerSnapshotDto Timer,
    ActiveSubstageContextDto? ActiveSubstage,
    IReadOnlyList<SubstageProgressDto> Substages,
    IReadOnlyList<VisibleClueDto> VisibleClues,
    IReadOnlyList<ActiveTargetDto> ActiveTargets);

public sealed record SubstageProgressDto(
    Guid SubstageSnapshotId,
    string Title,
    int SequenceOrder,
    string PlayMode,
    string Status);

public sealed record ActiveSubstageContextDto(
    Guid SubstageSnapshotId,
    string PlayMode,
    string Title,
    int TotalActiveTargets,
    int ResolvedTargets,
    int? ActiveQuestionSequenceOrder,
    int? ActiveQuestionTimeLimitSeconds,
    IReadOnlyList<ActiveSubstageTargetDto> Targets);

public sealed record ActiveSubstageTargetDto(
    Guid TargetSnapshotId,
    string Name,
    int SequenceOrder,
    bool HasHiddenClue);

public sealed record VisibleClueDto(
    Guid? TargetSnapshotId,
    Guid? ClueSnapshotId,
    Guid? OperativeClueId,
    string ClueText,
    string? TargetName);

public sealed record ActiveTargetDto(
    Guid TargetSnapshotId,
    string Name,
    int SequenceOrder,
    double Latitude,
    double Longitude);
