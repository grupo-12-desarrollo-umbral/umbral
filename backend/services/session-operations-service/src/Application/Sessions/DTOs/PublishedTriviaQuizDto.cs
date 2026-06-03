namespace umbral_backend.Application.Sessions.DTOs;

public sealed record PublishedTriviaQuizDto(
    int Id,
    string Title,
    string Status,
    IReadOnlyList<PublishedTriviaQuestionDto> Questions);

public sealed record PublishedTriviaQuestionDto(
    int Id,
    string Prompt,
    int SequenceOrder,
    bool IsActive,
    int ScoreValue,
    int TimeLimitSeconds,
    string? Explanation,
    IReadOnlyList<PublishedTriviaOptionDto> Options);

public sealed record PublishedTriviaOptionDto(
    int Id,
    string OptionText,
    int SequenceOrder,
    bool IsCorrect);
