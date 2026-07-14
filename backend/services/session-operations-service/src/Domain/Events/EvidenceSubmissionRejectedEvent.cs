using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

// Canon ddd_solution_model.md:328. Raised when an evidence submission transitions to Rejected,
// carrying the normalized rejection reason sourced from either EvidenceRejectionReason (contextual)
// or TargetResolutionRejectionReason (QR). One event per resolved submission, raised exactly once.
public sealed class EvidenceSubmissionRejectedEvent : BaseEvent
{
    public EvidenceSubmissionRejectedEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid evidenceSubmissionId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        DateTimeOffset submittedAt,
        string rejectionReason,
        DateTimeOffset resolvedAt)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EvidenceSubmissionId = evidenceSubmissionId;
        ActiveSubstageId = activeSubstageId;
        SubmissionType = submissionType;
        SubmittedAt = submittedAt;
        RejectionReason = rejectionReason;
        ResolvedAt = resolvedAt;
    }

    public Guid LiveSessionId { get; }
    public Guid TeamId { get; }
    public Guid EvidenceSubmissionId { get; }
    public Guid ActiveSubstageId { get; }
    public EvidenceSubmissionType SubmissionType { get; }
    public DateTimeOffset SubmittedAt { get; }
    public string RejectionReason { get; }
    public DateTimeOffset ResolvedAt { get; }
}
