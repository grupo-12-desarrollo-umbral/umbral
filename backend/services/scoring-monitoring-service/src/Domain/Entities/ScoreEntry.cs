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
        TeamDisplayName = string.Empty;
        ScoreValue = null!;
    }

    private ScoreEntry(
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid teamId,
        string teamDisplayName,
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
        TeamDisplayName = string.IsNullOrWhiteSpace(teamDisplayName) ? string.Empty : teamDisplayName.Trim();
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

    // Snapshotted at record time from the integration event so ranking recalculation can name rows
    // without an authenticated cross-service lookup from its (user-less) message-consumer context.
    public string TeamDisplayName { get; private set; }

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
        string teamDisplayName,
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
            teamDisplayName,
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

    // appliedAt is threaded in from the caller that created the sibling Penalty rather than read from
    // the clock here: the factory no longer mints the Penalty, so a second DateTimeOffset.UtcNow would
    // let the persisted row and ScoreEntryRegistered drift from the persisted Penalty.AppliedAt they
    // are meant to line up with.
    public static ScoreEntry Penalty(
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid teamId,
        string teamDisplayName,
        string reason,
        ScoreValue deductionValue,
        Guid penaltyId,
        DateTimeOffset appliedAt)
    {
        var entry = new ScoreEntry(
            scoreEntryId,
            liveSessionId,
            teamId,
            teamDisplayName,
            ScoreEntryType.Penalty,
            reason,
            deductionValue,
            appliedAt,
            ScoreSourceType.Penalty,
            penaltyId,
            null);

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

        // Sibling to the ledger fact: the ScoreEntryRegistered above recalculates the ranking, while this
        // event drives the explicit participant-facing penalty notification. Both must ride the same
        // ScoreEntry so they share one persistence transaction and one applied-at instant.
        entry.AddDomainEvent(new PenaltyApplied(
            penaltyId,
            entry.ScoreEntryId,
            entry.LiveSessionId,
            entry.TeamId,
            entry.ScoreValue.Value,
            entry.ReasonCode,
            entry.RecordedAt));

        return entry;
    }
}
