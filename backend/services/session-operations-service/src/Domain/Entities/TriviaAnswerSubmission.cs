using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

// Trivia specialization of EvidenceSubmission (bd_umbral_entity_spec.md:600): the one accepted team
// answer for a snapshotted trivia question. Created only by LiveSession's answer-registration
// skeleton, which snapshots correctness and the awarded score from the question option at accept time.
// It declares ONLY trivia-specific state; identity (EvidenceSubmissionId), the substage snapshot key
// (ActiveSubstageId), and acceptance (ValidationState) all live on the base and are never duplicated.
public sealed class TriviaAnswerSubmission : EvidenceSubmission
{
    private TriviaAnswerSubmission()
    {
    }

    private TriviaAnswerSubmission(
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        int questionSequenceOrder,
        int selectedOptionSequenceOrder,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt,
        bool isCorrect,
        int scoreValue)
        : base(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            activeSubstageId,
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId,
            submittedAt,
            EvidenceValidationState.Accepted)
    {
        QuestionSequenceOrder = questionSequenceOrder;
        SelectedOptionSequenceOrder = selectedOptionSequenceOrder;
        IsCorrect = isCorrect;
        ScoreValue = scoreValue;
    }

    // The snapshotted question is identified by (base ActiveSubstageId + this sequence order): the
    // frozen MissionRuntimeSnapshot carries no per-question Guid, so this pair is the stable key.
    public int QuestionSequenceOrder { get; private set; }

    public int SelectedOptionSequenceOrder { get; private set; }

    public bool IsCorrect { get; private set; }

    public int ScoreValue { get; private set; }

    // Accept factory — the only way to build a trivia answer. Correctness/score are already resolved
    // from the question snapshot by the LiveSession skeleton and passed in here.
    public static TriviaAnswerSubmission Accept(
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        int questionSequenceOrder,
        int selectedOptionSequenceOrder,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt,
        bool isCorrect,
        int scoreValue)
    {
        return new TriviaAnswerSubmission(
            liveSessionId,
            teamId,
            activeSubstageId,
            questionSequenceOrder,
            selectedOptionSequenceOrder,
            submittedByParticipantId,
            submittedAt,
            isCorrect,
            scoreValue);
    }
}
