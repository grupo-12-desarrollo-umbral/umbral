using MassTransit;

namespace umbral_backend.Application.Scores.Common;

/// <summary>
/// Service-local contract for the accepted-answer fact SessionOperations publishes after
/// transactional success (#165). It binds to the same broker exchange as the publisher through
/// <see cref="EntityNameAttribute"/> — MassTransit routes by that name, so this record and the
/// publisher's need only share the <c>session-answer-registered</c> entity name, not a shared
/// assembly. It carries correctness and the snapshotted score so ScoringMonitoring can compute
/// results downstream (HU-37).
/// </summary>
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
