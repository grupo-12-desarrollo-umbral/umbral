namespace umbral_backend.Domain.Events;

public sealed class TargetResolvedEvent : BaseEvent
{
    public TargetResolvedEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid evidenceSubmissionId,
        Guid activeSubstageId,
        Guid targetSnapshotId,
        int scoreValue,
        DateTimeOffset resolvedAt)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EvidenceSubmissionId = evidenceSubmissionId;
        ActiveSubstageId = activeSubstageId;
        TargetSnapshotId = targetSnapshotId;
        ScoreValue = scoreValue;
        ResolvedAt = resolvedAt;
    }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public Guid EvidenceSubmissionId { get; }

    public Guid ActiveSubstageId { get; }

    public Guid TargetSnapshotId { get; }

    public int ScoreValue { get; }

    public DateTimeOffset ResolvedAt { get; }
}
