namespace umbral_backend.Application.Dtos.Sessions;

public sealed record ParticipantTeamBoardDto(
    Guid LiveSessionId,
    Guid TeamId,
    string TeamDisplayName,
    string TeamCode,
    int CurrentScore,
    SessionTimerSnapshotDto Timer,
    ActiveSubstageContextDto? ActiveSubstage,
    IReadOnlyList<VisibleClueDto> VisibleClues);

public sealed record ActiveSubstageContextDto(
    Guid SubstageSnapshotId,
    string PlayMode,
    string Title,
    int TotalActiveTargets,
    int ResolvedTargets,
    int? ActiveQuestionSequenceOrder,
    int? ActiveQuestionTimeLimitSeconds);

public sealed record VisibleClueDto(
    Guid TargetSnapshotId,
    string ClueText,
    string TargetName);
