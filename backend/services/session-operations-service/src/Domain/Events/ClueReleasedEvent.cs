using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

public sealed class ClueReleasedEvent : BaseEvent
{
    public ClueReleasedEvent(
        Guid liveSessionId,
        Guid teamId,
        Guid? targetId,
        Guid? clueId,
        ReleaseMode releaseMode,
        int? releasedByUserId,
        DateTimeOffset releasedAt)
    {
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        TargetId = targetId;
        ClueId = clueId;
        ReleaseMode = releaseMode;
        ReleasedByUserId = releasedByUserId;
        ReleasedAt = releasedAt;
    }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public Guid? TargetId { get; }

    public Guid? ClueId { get; }

    public ReleaseMode ReleaseMode { get; }

    public int? ReleasedByUserId { get; }

    public DateTimeOffset ReleasedAt { get; }
}
