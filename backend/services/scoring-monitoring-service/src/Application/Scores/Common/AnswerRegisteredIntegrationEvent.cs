using MassTransit;

namespace umbral_backend.Application.Scores.Common;

// See TargetResolvedIntegrationEvent for the full rationale: the publisher declares this contract in
// `umbral_backend.Application.Sessions.Common`, so its message-type URN uses that namespace. Without
// pinning the URN here, MassTransit cannot match the deserialized message to this consumer and moves
// every event to the `_skipped` queue (trivia never scored). Keep in sync with the publisher.
// MassTransit prepends the `urn:message:` prefix itself, so supply only the namespace:type suffix.
[MessageUrn("umbral_backend.Application.Sessions.Common:AnswerRegisteredIntegrationEvent")]
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
