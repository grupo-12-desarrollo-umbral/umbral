namespace umbral_backend.Domain.Events;

public sealed class QuestionActivatedEvent : BaseEvent
{
    public QuestionActivatedEvent(
        Guid liveSessionId,
        int questionIndex,
        int sequenceOrder,
        int timeLimitSeconds,
        DateTimeOffset activatedAt)
    {
        LiveSessionId = liveSessionId;
        QuestionIndex = questionIndex;
        SequenceOrder = sequenceOrder;
        TimeLimitSeconds = timeLimitSeconds;
        ActivatedAt = activatedAt;
    }

    public Guid LiveSessionId { get; }

    public int QuestionIndex { get; }

    public int SequenceOrder { get; }

    public int TimeLimitSeconds { get; }

    public DateTimeOffset ActivatedAt { get; }
}
