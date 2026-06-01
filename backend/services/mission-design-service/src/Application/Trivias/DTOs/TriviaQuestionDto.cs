namespace umbral_backend.Application.Trivias.DTOs;

public sealed record TriviaQuestionDto(
    int Id,
    string Prompt,
    int SequenceOrder,
    bool IsActive,
    IReadOnlyList<TriviaOptionDto> Options);
