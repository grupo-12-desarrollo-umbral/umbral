using MassTransit;

namespace umbral_backend.Application.Scores.Common;

[EntityName("session-answer-registered")]
public sealed record AnswerRegisteredIntegrationEvent(
    Guid LiveSessionId,
    Guid TeamId,
    Guid TriviaAnswerSubmissionId,
    Guid TriviaSubstageSnapshotId,
    int QuestionSequenceOrder,
    int SelectedOptionSequenceOrder,
    bool IsCorrect,
    int ScoreValue,
    DateTimeOffset SubmittedAt);
