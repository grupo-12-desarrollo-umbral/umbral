namespace umbral_backend.Application.Trivias.Common;

public sealed record TriviaOptionDto(
    int Id,
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);
