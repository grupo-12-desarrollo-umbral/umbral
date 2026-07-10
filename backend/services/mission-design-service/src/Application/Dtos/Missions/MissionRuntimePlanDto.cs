namespace umbral_backend.Application.Dtos.Missions;

public sealed record MissionRuntimePlanDto(
    string Title,
    int MaximumTime,
    IReadOnlyList<MissionRuntimePlanStageDto> Stages);

public sealed record MissionRuntimePlanStageDto(
    string Title,
    int SequenceOrder,
    IReadOnlyList<MissionRuntimePlanSubstageDto> Substages);

public sealed record MissionRuntimePlanSubstageDto(
    string Title,
    int SequenceOrder,
    string PlayMode,
    IReadOnlyList<MissionRuntimePlanTargetDto> Targets,
    IReadOnlyList<MissionRuntimePlanTriviaQuestionDto> TriviaQuestions);

public sealed record MissionRuntimePlanTargetDto(
    string Name,
    string QrCode,
    int SequenceOrder,
    bool IsActive,
    int Score,
    MissionRuntimePlanClueDto? Clue);

public sealed record MissionRuntimePlanClueDto(
    string Text,
    string VisibilityPolicy);

public sealed record MissionRuntimePlanTriviaQuestionDto(
    string Prompt,
    int SequenceOrder,
    IReadOnlyList<MissionRuntimePlanTriviaOptionDto> Options,
    int ScoreValue,
    int TimeLimitSeconds);

public sealed record MissionRuntimePlanTriviaOptionDto(
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);
