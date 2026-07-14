using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

// Raised once after the shared evidence-intake invariants pass. Concrete evidence forms may raise
// additional facts after applying their own validation rules.
public sealed class EvidenceSubmissionRegisteredEvent : BaseEvent
{
    public EvidenceSubmissionRegisteredEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid evidenceSubmissionId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        DateTimeOffset submittedAt,
        EvidenceValidationState validationState,
        string? originReference = null)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EvidenceSubmissionId = evidenceSubmissionId;
        ActiveSubstageId = activeSubstageId;
        SubmissionType = submissionType;
        SubmittedAt = submittedAt;
        ValidationState = validationState;
        OriginReference = originReference;
    }

    public Guid LiveSessionId { get; }
    public Guid TeamId { get; }
    public Guid EvidenceSubmissionId { get; }
    public Guid ActiveSubstageId { get; }
    public EvidenceSubmissionType SubmissionType { get; }
    public DateTimeOffset SubmittedAt { get; }
    public EvidenceValidationState ValidationState { get; }

    // The origin grain (target, question, etc.) identifying the *"nodo de misión o pregunta de trivia"*
    // for traceability (AC #2). Derived per concrete form; null when the form does not contribute one.
    public string? OriginReference { get; }
}
