namespace umbral_backend.Application.Dtos.Sessions;

public sealed record TriviaAnswerReviewDto(
    Guid LiveSessionId,
    int QuestionSequenceOrder,
    IReadOnlyList<TriviaTeamAnswerReviewDto> Teams);

public sealed record TriviaTeamAnswerReviewDto(
    Guid TeamId,
    string TeamCode,
    string DisplayName,
    int? SelectedOptionSequenceOrder,
    bool? IsCorrect,
    int? ScoreValue,
    DateTimeOffset? AnsweredAt);
