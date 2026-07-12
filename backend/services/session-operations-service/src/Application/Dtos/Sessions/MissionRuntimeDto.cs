namespace umbral_backend.Application.Dtos.Sessions;

public sealed record MissionRuntimeDto(
    string Title,
    int MaximumTime,
    IReadOnlyList<MissionRuntimeStageDto> Stages);

public sealed record MissionRuntimeStageDto(
    string Title,
    int SequenceOrder,
    IReadOnlyList<MissionRuntimeSubstageDto> Substages);

public sealed record MissionRuntimeSubstageDto(
    string Title,
    int SequenceOrder,
    string PlayMode,
    IReadOnlyList<MissionRuntimeTargetDto> Targets,
    IReadOnlyList<MissionRuntimeTriviaQuestionDto> TriviaQuestions,
    IReadOnlyList<MissionRuntimeClueDto> Clues);

public sealed record MissionRuntimeTargetDto(
    string Name,
    string QrCode,
    int SequenceOrder,
    bool IsActive,
    int Score,
    double Latitude,
    double Longitude,
    MissionRuntimeClueDto? Clue);

public sealed record MissionRuntimeClueDto(
    string Text,
    string VisibilityPolicy);

public sealed record MissionRuntimeTriviaQuestionDto(
    string Prompt,
    int SequenceOrder,
    int ScoreValue,
    int TimeLimitSeconds,
    string? Explanation,
    IReadOnlyList<MissionRuntimeTriviaOptionDto> Options);

public sealed record MissionRuntimeTriviaOptionDto(
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);
