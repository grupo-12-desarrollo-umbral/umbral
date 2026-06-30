namespace umbral_backend.Application.Trivias.Queries.GetTriviaCatalog;

public sealed record TriviaQuizSummaryDto(
    int Id,
    string Title,
    string Description,
    string Status,
    int? SourceTriviaQuizId = null,
    bool HasUsageHistory = false,
    bool IsDuplicate = false);
