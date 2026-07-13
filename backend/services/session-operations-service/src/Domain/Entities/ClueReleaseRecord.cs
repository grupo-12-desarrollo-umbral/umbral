using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

public sealed class ClueReleaseRecord : BaseEntity
{
    private ClueReleaseRecord()
    {
        ClueReleaseRecordId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
        TargetId = Guid.Empty;
    }

    private ClueReleaseRecord(
        Guid clueReleaseRecordId,
        Guid liveSessionId,
        Guid teamId,
        Guid targetId,
        Guid? clueId,
        ReleaseMode releaseMode,
        int? releasedByUserId,
        DateTimeOffset releasedAt)
    {
        ClueReleaseRecordId = clueReleaseRecordId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        TargetId = targetId;
        ClueId = clueId;
        ReleaseMode = releaseMode;
        ReleasedByUserId = releasedByUserId;
        ReleasedAt = releasedAt;
    }

    public Guid ClueReleaseRecordId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid? ClueId { get; private set; }

    public ReleaseMode ReleaseMode { get; private set; }

    public int? ReleasedByUserId { get; private set; }

    public DateTimeOffset ReleasedAt { get; private set; }

    internal static ClueReleaseRecord CreateManual(
        Guid liveSessionId,
        Guid teamId,
        Guid targetId,
        Guid? clueId,
        int operatorUserId,
        DateTimeOffset releasedAt)
    {
        return new ClueReleaseRecord(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            targetId,
            clueId,
            ReleaseMode.Manual,
            operatorUserId,
            releasedAt);
    }
}
