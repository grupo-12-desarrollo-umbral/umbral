namespace umbral_backend.Application.Dtos.Sessions;

// Accepted-answer result (HU-34). Acceptance metadata ONLY: it deliberately omits IsCorrect,
// ScoreValue, and the selected option so correctness/points never leak to the participant. Fairness
// keeps those internal until close (HU-35) / downstream scoring (HU-37); they travel only on the
// RabbitMQ AnswerRegistered contract.
public sealed record SubmitTriviaAnswerResultDto(
    Guid LiveSessionId,
    Guid TeamId,
    Guid TriviaSubstageSnapshotId,
    int QuestionSequenceOrder,
    DateTimeOffset AnsweredAt);
