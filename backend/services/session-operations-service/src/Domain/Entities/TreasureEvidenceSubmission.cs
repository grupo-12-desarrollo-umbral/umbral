using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

public sealed class TreasureEvidenceSubmission : EvidenceSubmission
{
    private TreasureEvidenceSubmission()
    {
        ScannedValue = string.Empty;
    }

    private TreasureEvidenceSubmission(
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        string scannedValue,
        Guid? targetSnapshotId,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt)
        : base(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            activeSubstageId,
            EvidenceSubmissionType.TreasureHuntQrScan,
            submittedByParticipantId,
            submittedAt)
    {
        ScannedValue = scannedValue;
        TargetSnapshotId = targetSnapshotId;
    }

    public string ScannedValue { get; private set; }

    public Guid? TargetSnapshotId { get; private set; }

    public TargetResolutionRejectionReason? ResolutionRejectionReason { get; private set; }

    public static TreasureEvidenceSubmission Accept(
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        string scannedValue,
        Guid targetSnapshotId,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt)
    {
        var submission = Begin(
            liveSessionId,
            teamId,
            activeSubstageId,
            scannedValue,
            targetSnapshotId,
            submittedByParticipantId,
            submittedAt);

        submission.AcceptRegisteredTarget();
        return submission;
    }

    internal static TreasureEvidenceSubmission Begin(
        Guid liveSessionId,
        Guid teamId,
        Guid activeSubstageId,
        string scannedValue,
        Guid? targetSnapshotId,
        Guid submittedByParticipantId,
        DateTimeOffset submittedAt)
    {
        return new TreasureEvidenceSubmission(
            liveSessionId,
            teamId,
            activeSubstageId,
            scannedValue,
            targetSnapshotId,
            submittedByParticipantId,
            submittedAt);
    }

    internal void AcceptRegisteredTarget()
    {
        MarkAcceptedByConcreteForm();
    }

    internal void RejectRegisteredTarget(TargetResolutionRejectionReason reason)
    {
        ResolutionRejectionReason = reason;
        MarkRejectedByConcreteForm();
    }
}
