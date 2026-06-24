namespace umbral_backend.Application.Trivias.Common;

public sealed record TriviaQuestionDto(
    int Id,
    string Prompt,
    int SequenceOrder,
    bool IsActive,
    IReadOnlyList<TriviaOptionDto> Options,
    int? ScoreValue = null,
    int? TimeLimitSeconds = null,
    string? Explanation = null);
