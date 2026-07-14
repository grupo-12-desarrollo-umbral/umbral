using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class ScoreEntry : BaseAuditableEntity
{
    private ScoreEntry()
    {
        ScoreEntryId = Guid.Empty;
        ReasonCode = string.Empty;
        ScoreValue = null!;
    }

    private ScoreEntry(
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid teamId,
        ScoreEntryType entryType,
        string reasonCode,
        ScoreValue scoreValue,
        DateTimeOffset recordedAt,
        ScoreSourceType sourceEntityType,
        Guid sourceEntityId,
        int? recordedByUserId)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new ArgumentException("Reason code is required.", nameof(reasonCode));
        }

        ArgumentNullException.ThrowIfNull(scoreValue);

        if (sourceEntityId == Guid.Empty)
        {
            throw new ArgumentException("Source entity id is required.", nameof(sourceEntityId));
        }

        ScoreEntryId = scoreEntryId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        EntryType = entryType;
        ReasonCode = reasonCode.Trim();
        ScoreValue = scoreValue;
        RecordedAt = recordedAt;
        SourceEntityType = sourceEntityType;
        SourceEntityId = sourceEntityId;
        RecordedByUserId = recordedByUserId;
    }

    public Guid ScoreEntryId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid TeamId { get; private set; }

    public ScoreEntryType EntryType { get; private set; }

    public string ReasonCode { get; private set; }

    public ScoreValue ScoreValue { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public ScoreSourceType SourceEntityType { get; private set; }

    public Guid SourceEntityId { get; private set; }

    public int? RecordedByUserId { get; private set; }

    public static ScoreEntry Grant(
        Guid liveSessionId,
        Guid teamId,
        string reasonCode,
        ScoreValue scoreValue,
        DateTimeOffset recordedAt,
        ScoreSourceType sourceEntityType,
        Guid sourceEntityId,
        int? recordedByUserId = null)
    {
        var entry = new ScoreEntry(
            Guid.NewGuid(),
            liveSessionId,
            teamId,
            ScoreEntryType.Grant,
            reasonCode,
            scoreValue,
            recordedAt,
            sourceEntityType,
            sourceEntityId,
            recordedByUserId);

        entry.AddDomainEvent(new ScoreEntryRegistered(
            entry.ScoreEntryId,
            entry.LiveSessionId,
            entry.TeamId,
            entry.EntryType,
            entry.ReasonCode,
            entry.ScoreValue.Value,
            entry.RecordedAt,
            entry.SourceEntityType,
            entry.SourceEntityId,
            entry.RecordedByUserId));

        return entry;
    }
}
