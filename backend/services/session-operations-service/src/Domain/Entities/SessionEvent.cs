using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

public sealed class SessionEvent : BaseEntity
{
    private const string StateChangedEventType = "SessionStateChanged";

    private SessionEvent()
    {
        SessionEventId = Guid.Empty;
        LiveSessionId = Guid.Empty;
        EventType = string.Empty;
        PayloadSummary = string.Empty;
        CorrelationId = Guid.Empty;
    }

    private SessionEvent(
        Guid liveSessionId,
        SessionState previousState,
        SessionState currentState,
        DateTimeOffset occurredAt,
        SessionEventActorType actorType,
        int? actorId,
        string? reason)
    {
        SessionEventId = Guid.NewGuid();
        LiveSessionId = liveSessionId;
        OccurredAt = occurredAt;
        ActorType = actorType;
        ActorId = actorId;
        EventType = StateChangedEventType;
        PayloadSummary = BuildStateChangeSummary(previousState, currentState, reason);
        CorrelationId = Guid.NewGuid();
    }

    public Guid SessionEventId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public SessionEventActorType ActorType { get; private set; }

    public int? ActorId { get; private set; }

    public string EventType { get; private set; }

    public string PayloadSummary { get; private set; }

    public Guid CorrelationId { get; private set; }

    public static SessionEvent ForStateChange(
        Guid liveSessionId,
        SessionState previousState,
        SessionState currentState,
        DateTimeOffset occurredAt,
        SessionEventActorType actorType,
        int? actorId,
        string? reason = null)
    {
        return new SessionEvent(
            liveSessionId,
            previousState,
            currentState,
            occurredAt,
            actorType,
            actorId,
            NormalizeReason(reason));
    }

    private static string BuildStateChangeSummary(
        SessionState previousState,
        SessionState currentState,
        string? reason)
    {
        var transition = $"{previousState}→{currentState}";
        return reason is null ? transition : $"{transition}: {reason}";
    }

    private static string? NormalizeReason(string? reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
