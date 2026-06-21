namespace umbral_backend.Application.Missions.DTOs;

public sealed record MissionRuntimePlanDto(
    string Title,
    int MaximumTime,
    IReadOnlyList<MissionRuntimePlanStageDto> Stages);

public sealed record MissionRuntimePlanStageDto(
    IReadOnlyList<MissionRuntimePlanSubstageDto> Substages);

public sealed record MissionRuntimePlanSubstageDto(
    string PlayMode,
    int? WinnerScore,
    IReadOnlyList<MissionRuntimePlanTargetDto> Targets,
    IReadOnlyList<MissionRuntimePlanTriviaQuestionDto> TriviaQuestions);

public sealed record MissionRuntimePlanTargetDto(
    string Name,
    string QrCode,
    int SequenceOrder,
    bool IsActive,
    MissionRuntimePlanClueDto? Clue);

public sealed record MissionRuntimePlanClueDto(
    string Text,
    string VisibilityPolicy);

public sealed record MissionRuntimePlanTriviaQuestionDto(
    string Prompt,
    IReadOnlyList<MissionRuntimePlanTriviaOptionDto> Options,
    int ScoreValue,
    int TimeLimitSeconds);

public sealed record MissionRuntimePlanTriviaOptionDto(
    string OptionText,
    bool IsCorrect);
