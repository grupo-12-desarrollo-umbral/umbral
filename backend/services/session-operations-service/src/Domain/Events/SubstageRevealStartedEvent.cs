using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

// Raised when a substage ends and its reveal window opens (D-3): a treasure hunt cleared by its first
// team, or a trivia substage out of questions. IsTerminal marks the last substage, whose reveal the
// mission finishes on rather than advancing off.
//
// Deliberately NOT named "...RankingReveal..." like the DTO it feeds: the domain fact here is that the
// substage is revealing, and it carries no ranking at all (scoring/ranking are scoring-monitoring's,
// per HU-33B D-2 — a pin that LiveSessionTests enforces by name over this assembly). That clients
// spend the window showing the ranking they already hold is a contract-layer concern, which is where
// SubstageRankingRevealStartedNotificationDto says so.
public sealed class SubstageRevealStartedEvent : BaseEvent
{
    public SubstageRevealStartedEvent(
        Guid liveSessionId,
        Guid substageSnapshotId,
        SubstagePlayMode playMode,
        DateTimeOffset revealUntil,
        bool isTerminal,
        DateTimeOffset occurredAt)
    {
        LiveSessionId = liveSessionId;
        SubstageSnapshotId = substageSnapshotId;
        PlayMode = playMode;
        RevealUntil = revealUntil;
        IsTerminal = isTerminal;
        OccurredAt = occurredAt;
    }

    public Guid LiveSessionId { get; }

    public Guid SubstageSnapshotId { get; }

    public SubstagePlayMode PlayMode { get; }

    public DateTimeOffset RevealUntil { get; }

    public bool IsTerminal { get; }

    public DateTimeOffset OccurredAt { get; }
}
