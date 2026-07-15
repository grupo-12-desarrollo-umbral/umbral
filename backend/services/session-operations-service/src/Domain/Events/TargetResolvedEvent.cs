namespace umbral_backend.Domain.Events;

public sealed class TargetResolvedEvent : BaseEvent
{
    public TargetResolvedEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid referenceTeamId,
        string teamDisplayName,
        Guid evidenceSubmissionId,
        Guid activeSubstageId,
        Guid targetSnapshotId,
        int scoreValue,
        DateTimeOffset resolvedAt)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        ReferenceTeamId = referenceTeamId;
        TeamDisplayName = teamDisplayName;
        EvidenceSubmissionId = evidenceSubmissionId;
        ActiveSubstageId = activeSubstageId;
        TargetSnapshotId = targetSnapshotId;
        ScoreValue = scoreValue;
        ResolvedAt = resolvedAt;
    }

    public Guid LiveSessionId { get; }

    // Session-scoped team id — retained for operator-facing consumers that key on it.
    public Guid TeamId { get; }

    // Cross-context catalog team id — the identity scoring/ranking keys on (mobile + seed use it too).
    public Guid ReferenceTeamId { get; }

    // Snapshotted display name so downstream scoring can name ranking rows without an authenticated
    // cross-service lookup from its (user-less) message-consumer context.
    public string TeamDisplayName { get; }

    public Guid EvidenceSubmissionId { get; }

    public Guid ActiveSubstageId { get; }

    public Guid TargetSnapshotId { get; }

    public int ScoreValue { get; }

    public DateTimeOffset ResolvedAt { get; }
}
