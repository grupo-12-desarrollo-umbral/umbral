using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

// Canon ddd_solution_model.md:327. Raised when an evidence submission transitions to Accepted
// via MarkAcceptedByConcreteForm. One event per resolved submission, raised exactly once.
public sealed class EvidenceSubmissionAcceptedEvent : BaseEvent
{
    public EvidenceSubmissionAcceptedEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid evidenceSubmissionId,
        Guid activeSubstageId,
        EvidenceSubmissionType submissionType,
        DateTimeOffset submittedAt,
        DateTimeOffset resolvedAt)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EvidenceSubmissionId = evidenceSubmissionId;
        ActiveSubstageId = activeSubstageId;
        SubmissionType = submissionType;
        SubmittedAt = submittedAt;
        ResolvedAt = resolvedAt;
    }

    public Guid LiveSessionId { get; }
    public Guid TeamId { get; }
    public Guid EvidenceSubmissionId { get; }
    public Guid ActiveSubstageId { get; }
    public EvidenceSubmissionType SubmissionType { get; }
    public DateTimeOffset SubmittedAt { get; }
    public DateTimeOffset ResolvedAt { get; }
}
