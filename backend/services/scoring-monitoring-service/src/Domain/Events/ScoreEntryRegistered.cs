using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

public sealed class ScoreEntryRegistered : BaseEvent
{
    public ScoreEntryRegistered(
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid teamId,
        ScoreEntryType entryType,
        string reasonCode,
        int scoreValue,
        DateTimeOffset recordedAt,
        ScoreSourceType sourceEntityType,
        Guid sourceEntityId,
        int? recordedByUserId)
    {
        ScoreEntryId = scoreEntryId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EntryType = entryType;
        ReasonCode = reasonCode;
        ScoreValue = scoreValue;
        RecordedAt = recordedAt;
        SourceEntityType = sourceEntityType;
        SourceEntityId = sourceEntityId;
        RecordedByUserId = recordedByUserId;
    }

    public Guid ScoreEntryId { get; }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public ScoreEntryType EntryType { get; }

    public string ReasonCode { get; }

    public int ScoreValue { get; }

    public DateTimeOffset RecordedAt { get; }

    public ScoreSourceType SourceEntityType { get; }

    public Guid SourceEntityId { get; }

    public int? RecordedByUserId { get; }
}
