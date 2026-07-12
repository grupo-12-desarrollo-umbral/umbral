namespace umbral_backend.Application.Dtos.Missions;

public sealed record MissionDto(
    int Id,
    string Name,
    string Description,
    string Difficulty,
    int MaximumTimeMinutes,
    string Status,
    IReadOnlyList<MissionStageDto>? Stages = null);

public sealed record MissionStageDto(
    int Id,
    string Title,
    int SequenceOrder,
    IReadOnlyList<MissionSubstageDto>? Substages = null);

public sealed record MissionSubstageDto(
    int Id,
    string Title,
    int SequenceOrder,
    string PlayMode,
    TriviaQuizSelectionDto? TriviaQuizSelection,
    IReadOnlyList<MissionTargetDto>? Targets = null,
    IReadOnlyList<MissionClueDto>? Clues = null);

public sealed record MissionTargetDto(
    int Id,
    string Name,
    string QrCode,
    int SequenceOrder,
    bool IsActive,
    int? ClueId,
    int Score,
    double Latitude,
    double Longitude);

public sealed record MissionClueDto(
    int Id,
    string Title,
    int SequenceOrder,
    string Text,
    string VisibilityPolicy);

public sealed record TriviaQuizSelectionDto(
    int TriviaQuizId);
