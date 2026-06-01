namespace umbral_backend.Application.Trivias.DTOs;

public sealed record TriviaOptionDto(
    int Id,
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);
