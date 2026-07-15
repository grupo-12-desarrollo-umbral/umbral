using MassTransit;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Accepted-answer fact published to RabbitMQ after transactional success (HU-34) on the
/// <c>session-answer-registered</c> exchange named via <see cref="EntityNameAttribute"/>, consumed
/// downstream by ScoringMonitoring. Unlike the participant response and the operator signal, this
/// contract DELIBERATELY carries correctness and the snapshotted score so scoring can compute
/// results — it never crosses the participant boundary.
/// </summary>
[EntityName("session-answer-registered")]
public sealed record AnswerRegisteredIntegrationEvent(
    Guid LiveSessionId,
    Guid TeamId,
    Guid ReferenceTeamId,
    string TeamDisplayName,
    Guid TriviaAnswerSubmissionId,
    Guid TriviaSubstageSnapshotId,
    int QuestionSequenceOrder,
    int SelectedOptionSequenceOrder,
    bool IsCorrect,
    int ScoreValue,
    DateTimeOffset SubmittedAt);
