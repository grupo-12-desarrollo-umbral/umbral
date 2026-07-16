namespace umbral_backend.Application.Dtos.Sessions;

public sealed record TriviaTeamQuestionResultDto(
    int? SelectedOptionSequenceOrder,
    bool? IsCorrect,
    int? ScoreValue,
    int CorrectOptionSequenceOrder,
    string? Explanation);
