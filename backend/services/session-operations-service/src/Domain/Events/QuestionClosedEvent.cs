namespace umbral_backend.Domain.Events;

public sealed class QuestionClosedEvent : BaseEvent
{
    public QuestionClosedEvent(
        Guid liveSessionId,
        int questionIndex,
        DateTimeOffset closedAt,
        bool wasExpiredByTimer)
    {
        LiveSessionId = liveSessionId;
        QuestionIndex = questionIndex;
        ClosedAt = closedAt;
        WasExpiredByTimer = wasExpiredByTimer;
    }

    public Guid LiveSessionId { get; }

    public int QuestionIndex { get; }

    public DateTimeOffset ClosedAt { get; }

    public bool WasExpiredByTimer { get; }
}
