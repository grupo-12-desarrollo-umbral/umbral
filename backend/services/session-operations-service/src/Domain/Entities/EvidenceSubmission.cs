using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

// Umbrella base for a team's evidence in the active mission substage (bd_umbral_entity_spec.md:401).
// It captures the generic submission identity/context; concrete forms — TriviaAnswerSubmission now,
// the treasure-hunt QR form later — specialize it with their own fields, snapshots, and events.
// Abstract: an evidence row only ever exists through one of its specializations.
public abstract class EvidenceSubmission : BaseEntity
{
    protected EvidenceSubmission()
    {
        EvidenceSubmissionId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
        ActiveSubstageId = Guid.Empty;
    }

    protected EvidenceSubmission(
        Guid evidenceSubmissionId,
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        Guid? submittedByParticipantId,
        DateTimeOffset submittedAt,
        EvidenceValidationState validationState)
    {
        if (liveSessionId == Guid.Empty || teamId == Guid.Empty || activeSubstageId == Guid.Empty)
        {
            throw new EvidenceSubmissionContextRequiredException();
        }

        if (submittedAt == default)
        {
            throw new EvidenceSubmissionTimestampRequiredException();
        }

        EvidenceSubmissionId = evidenceSubmissionId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        ActiveSubstageId = activeSubstageId;
        SubmissionType = submissionType;
        SubmittedByParticipantId = submittedByParticipantId;
        SubmittedAt = submittedAt;
        ValidationState = validationState;
    }

    protected EvidenceSubmission(
        Guid evidenceSubmissionId,
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        Guid? submittedByParticipantId,
        DateTimeOffset submittedAt)
        : this(
            evidenceSubmissionId,
            liveSessionId,
            teamId,
            activeSubstageId,
            submissionType,
            submittedByParticipantId,
            submittedAt,
            EvidenceValidationState.Pending)
    {
    }

    public Guid EvidenceSubmissionId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid ActiveSubstageId { get; private set; }

    public EvidenceSubmissionType SubmissionType { get; private set; }

    public Guid? SubmittedByParticipantId { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public EvidenceValidationState ValidationState { get; private set; }

    // Contextual rejection reason owned by the HU-30 validation path (Reject below).
    // Concrete forms with their own typed reason (e.g. treasure target resolution) do not
    // set this; they flip state via MarkRejectedByConcreteForm and keep their own reason.
    public EvidenceRejectionReason? RejectionReason { get; private set; }

    public void Reject(EvidenceRejectionReason reason)
    {
        if (ValidationState != EvidenceValidationState.Pending)
        {
            throw new EvidenceAlreadyResolvedException(ValidationState);
        }

        ValidationState = EvidenceValidationState.Rejected;
        RejectionReason = reason;

        AddDomainEvent(new EvidenceSubmissionRejectedEvent(
            LiveSessionId,
            TeamId,
            EvidenceSubmissionId,
            ActiveSubstageId,
            SubmissionType,
            SubmittedAt,
            reason.ToString(),
            SubmittedAt));
    }

    protected void MarkAcceptedByConcreteForm(DateTimeOffset resolvedAt)
    {
        ValidationState = EvidenceValidationState.Accepted;

        AddDomainEvent(new EvidenceSubmissionAcceptedEvent(
            LiveSessionId,
            TeamId,
            EvidenceSubmissionId,
            ActiveSubstageId,
            SubmissionType,
            SubmittedAt,
            resolvedAt));
    }

    protected void MarkRejectedByConcreteForm(string reason, DateTimeOffset resolvedAt)
    {
        ValidationState = EvidenceValidationState.Rejected;

        AddDomainEvent(new EvidenceSubmissionRejectedEvent(
            LiveSessionId,
            TeamId,
            EvidenceSubmissionId,
            ActiveSubstageId,
            SubmissionType,
            SubmittedAt,
            reason,
            resolvedAt));
    }

    // The origin grain that identifies the "nodo de misión o pregunta de trivia" for
    // traceability (AC #2). Each concrete form provides its own grain.
    public abstract string? DescribeOrigin();
}
