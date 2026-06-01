namespace umbral_backend.Application.Trivias.DTOs;

public sealed record TriviaQuizDto(
    int Id,
    string Title,
    string Description,
    string Status,
    IReadOnlyList<TriviaQuestionDto> Questions);
