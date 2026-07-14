namespace umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;

/// <summary>
/// Internal command the <c>AnswerRegisteredConsumer</c> dispatches once it has mapped the inbound
/// integration event (ADR-0017: the consumer is a thin transport adapter; all in-process work
/// happens here). This slice records receipt only — no <c>ScoreEntry</c> is written yet (HU-37).
/// </summary>
public sealed record RecordAnswerReceiptCommand(
    Guid LiveSessionId,
    Guid TeamId,
    Guid TriviaAnswerSubmissionId,
    Guid TriviaSubstageSnapshotId,
    int QuestionSequenceOrder,
    int SelectedOptionSequenceOrder,
    bool IsCorrect,
    int ScoreValue,
    DateTimeOffset SubmittedAt) : IRequest;
