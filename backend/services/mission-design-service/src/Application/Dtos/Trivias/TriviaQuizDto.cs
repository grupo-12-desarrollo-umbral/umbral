namespace umbral_backend.Application.Dtos.Trivias;

public sealed record TriviaQuizDto(
    int Id,
    string Title,
    string Description,
    string Status,
    IReadOnlyList<TriviaQuestionDto> Questions,
    int? SourceTriviaQuizId = null,
    bool HasUsageHistory = false,
    bool IsDuplicate = false);
