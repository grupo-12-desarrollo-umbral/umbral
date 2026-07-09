namespace umbral_backend.Application.Dtos.Trivias;

public sealed record TriviaQuizSummaryDto(
    int Id,
    string Title,
    string Description,
    string Status,
    int? SourceTriviaQuizId = null,
    bool HasUsageHistory = false,
    bool IsDuplicate = false);
