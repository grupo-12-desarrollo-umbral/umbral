using umbral_backend.Domain.Enums;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class ClueReleaseRecord : BaseEntity
{
    private ClueReleaseRecord()
    {
        ClueReleaseRecordId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        TeamId = Guid.Empty;
        TargetId = null;
    }

    private ClueReleaseRecord(
        Guid clueReleaseRecordId,
        Guid liveSessionId,
        Guid teamId,
        ClueReleaseSubject subject,
        ReleaseMode releaseMode,
        int? releasedByUserId,
        DateTimeOffset releasedAt)
    {
        ClueReleaseRecordId = clueReleaseRecordId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        TargetId = subject.TargetId;
        ClueId = subject.ClueId;
        ReleaseMode = releaseMode;
        ReleasedByUserId = releasedByUserId;
        ReleasedAt = releasedAt;
    }

    public Guid ClueReleaseRecordId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid? TargetId { get; private set; }

    public Guid? ClueId { get; private set; }

    public ReleaseMode ReleaseMode { get; private set; }

    public int? ReleasedByUserId { get; private set; }

    public DateTimeOffset ReleasedAt { get; private set; }

    internal static ClueReleaseRecord CreateManual(
        Guid liveSessionId,
        Guid teamId,
        ClueReleaseSubject subject,
        int operatorUserId,
        DateTimeOffset releasedAt)
    {
        return new ClueReleaseRecord(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            subject,
            ReleaseMode.Manual,
            operatorUserId,
            releasedAt);
    }
}
