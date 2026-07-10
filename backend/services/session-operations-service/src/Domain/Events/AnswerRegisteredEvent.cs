namespace umbral_backend.Domain.Events;

// Raised ONLY when a team's first in-time answer to the active trivia question is accepted and
// persisted (never on a rejected attempt). Carries the base evidence identity + substage snapshot and
// the correctness/score snapshot so downstream scoring can consume it; the operator-facing signal
// drops the correctness/score fields to avoid leakage.
public sealed class AnswerRegisteredEvent : BaseEvent
{
    public AnswerRegisteredEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid evidenceSubmissionId,
        Guid activeSubstageId,
        int questionSequenceOrder,
        int selectedOptionSequenceOrder,
        bool isCorrect,
        int scoreValue,
        DateTimeOffset submittedAt)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EvidenceSubmissionId = evidenceSubmissionId;
        ActiveSubstageId = activeSubstageId;
        QuestionSequenceOrder = questionSequenceOrder;
        SelectedOptionSequenceOrder = selectedOptionSequenceOrder;
        IsCorrect = isCorrect;
        ScoreValue = scoreValue;
        SubmittedAt = submittedAt;
    }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public Guid EvidenceSubmissionId { get; }

    public Guid ActiveSubstageId { get; }

    public int QuestionSequenceOrder { get; }

    public int SelectedOptionSequenceOrder { get; }

    public bool IsCorrect { get; }

    public int ScoreValue { get; }

    public DateTimeOffset SubmittedAt { get; }
}
