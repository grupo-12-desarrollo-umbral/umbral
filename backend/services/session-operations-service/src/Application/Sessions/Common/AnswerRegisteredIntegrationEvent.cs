namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Accepted-answer fact published to RabbitMQ after transactional success (HU-34, routing key
/// <c>session.answer.registered</c>), consumed downstream by ScoringMonitoring. Unlike the
/// participant response and the operator signal, this contract DELIBERATELY carries correctness and
/// the snapshotted score so scoring can compute results — it never crosses the participant boundary.
/// </summary>
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
