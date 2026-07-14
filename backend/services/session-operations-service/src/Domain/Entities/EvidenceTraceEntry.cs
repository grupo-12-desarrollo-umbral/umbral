using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

// Evidence traceability projection (derived name — canon silent; see hu32-context.md Known quirks).
// One row per registered EvidenceSubmission, keyed by the natural key EvidenceSubmissionId.
// Fed asynchronously by consumers of the domain outbox facts. Not an aggregate root.
// reviewer fields excluded per ADR-0010; override of hu30-context.md:26,87
public sealed class EvidenceTraceEntry : BaseEntity
{
    private EvidenceTraceEntry()
    {
        EvidenceSubmissionId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
        ActiveSubstageId = Guid.Empty;
    }

    private EvidenceTraceEntry(
        Guid evidenceSubmissionId,
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        Guid? submittedByParticipantId,
        string? originReference,
        DateTimeOffset submittedAt)
    {
        EvidenceSubmissionId = evidenceSubmissionId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        ActiveSubstageId = activeSubstageId;
        SubmissionType = submissionType;
        SubmittedByParticipantId = submittedByParticipantId;
        OriginReference = originReference;
        SubmittedAt = submittedAt;
        ValidationState = EvidenceValidationState.Pending;
    }

    public Guid EvidenceSubmissionId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid ActiveSubstageId { get; private set; }

    public EvidenceSubmissionType SubmissionType { get; private set; }

    public Guid? SubmittedByParticipantId { get; private set; }

    public string? OriginReference { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public EvidenceValidationState ValidationState { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public static EvidenceTraceEntry ForRegistration(
        Guid evidenceSubmissionId,
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        Guid? submittedByParticipantId,
        string? originReference,
        DateTimeOffset submittedAt)
    {
        return new EvidenceTraceEntry(
            evidenceSubmissionId,
            liveSessionId,
            teamId,
            activeSubstageId,
            submissionType,
            submittedByParticipantId,
            originReference,
            submittedAt);
    }

    public void MarkAccepted(DateTimeOffset resolvedAt)
    {
        if (ValidationState == EvidenceValidationState.Accepted)
        {
            return;
        }

        ValidationState = EvidenceValidationState.Accepted;
        RejectionReason = null;
        ResolvedAt = resolvedAt;
    }

    public void MarkRejected(string reason, DateTimeOffset resolvedAt)
    {
        if (ValidationState == EvidenceValidationState.Rejected)
        {
            return;
        }

        ValidationState = EvidenceValidationState.Rejected;
        RejectionReason = reason;
        ResolvedAt = resolvedAt;
    }
}
