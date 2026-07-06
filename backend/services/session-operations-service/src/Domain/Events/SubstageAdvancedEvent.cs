using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Events;

// Raised on each timer-driven substage advancement (ADR-0005). ToSubstageId is null when the final
// substage completed (session completion). ScoringMonitoring (HU-37A) consumes this to derive the
// TriviaSubstageWinner — session-operations emits the fact, it does not compute the winner.
public sealed class SubstageAdvancedEvent : BaseEvent
{
    public SubstageAdvancedEvent(
        Guid liveSessionId,
        Guid fromSubstageId,
        SubstagePlayMode fromPlayMode,
        Guid? toSubstageId,
        DateTimeOffset advancedAt)
    {
        LiveSessionId = liveSessionId;
        FromSubstageId = fromSubstageId;
        FromPlayMode = fromPlayMode;
        ToSubstageId = toSubstageId;
        AdvancedAt = advancedAt;
    }

    public Guid LiveSessionId { get; }

    public Guid FromSubstageId { get; }

    public SubstagePlayMode FromPlayMode { get; }

    public Guid? ToSubstageId { get; }

    public DateTimeOffset AdvancedAt { get; }
}
