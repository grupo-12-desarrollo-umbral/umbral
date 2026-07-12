namespace umbral_backend.Application.Dtos.Trivias;

public sealed record TriviaQuestionDto(
    int Id,
    string Prompt,
    bool IsActive,
    IReadOnlyList<TriviaOptionDto> Options,
    int? ScoreValue = null,
    int? TimeLimitSeconds = null,
    string? Explanation = null);
