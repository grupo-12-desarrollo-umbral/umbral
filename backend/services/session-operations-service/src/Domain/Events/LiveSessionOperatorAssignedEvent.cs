namespace umbral_backend.Domain.Events;

public sealed class LiveSessionOperatorAssignedEvent : BaseEvent
{
    public LiveSessionOperatorAssignedEvent(
        Guid liveSessionId,
        int? previousOperatorUserId,
        int? assignedOperatorUserId,
        string? assignedOperatorExternalId,
        DateTimeOffset occurredAt)
    {
        LiveSessionId = liveSessionId;
        PreviousOperatorUserId = previousOperatorUserId;
        AssignedOperatorUserId = assignedOperatorUserId;
        AssignedOperatorExternalId = assignedOperatorExternalId;
        OccurredAt = occurredAt;
    }

    public Guid LiveSessionId { get; }

    public int? PreviousOperatorUserId { get; }

    public int? AssignedOperatorUserId { get; }

    /// <summary>
    /// The assigned operator's external identity id (Keycloak sub). Carried only for the
    /// ScoringMonitoring authorization projection — the aggregate itself persists the internal numeric
    /// <see cref="AssignedOperatorUserId"/> (ADR 0009). Null when the assignment was made without a
    /// resolved external identity (e.g. test seeding); the integration publisher skips those.
    /// </summary>
    public string? AssignedOperatorExternalId { get; }

    public DateTimeOffset OccurredAt { get; }
}
