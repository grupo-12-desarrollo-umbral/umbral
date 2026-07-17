namespace umbral_backend.Domain.Events;

// Raised when MaximumTime runs out and the mission ends where it stands (D-4), in either play mode.
// ActiveSubstageId is the substage play was in when the clock ran out — it did not complete, which is
// why this is not a SubstageAdvancedEvent. Paired with the Finished state change in the same call.
public sealed class MissionDeadlineReachedEvent : BaseEvent
{
    public MissionDeadlineReachedEvent(
        Guid liveSessionId,
        Guid? activeSubstageId,
        DateTimeOffset occurredAt)
    {
        LiveSessionId = liveSessionId;
        ActiveSubstageId = activeSubstageId;
        OccurredAt = occurredAt;
    }

    public Guid LiveSessionId { get; }

    public Guid? ActiveSubstageId { get; }

    public DateTimeOffset OccurredAt { get; }
}
