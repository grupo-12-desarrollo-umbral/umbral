namespace umbral_backend.Application.Dtos.Trivias;

public sealed record TriviaOptionDto(
    int Id,
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);
