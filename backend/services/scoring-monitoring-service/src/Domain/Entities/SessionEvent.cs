using System.Globalization;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

public sealed class SessionEvent
{
    public const string StateChangedEventType = "SessionStateChanged";
    public const string QuestionClosedEventType = "QuestionClosed";
    public const string ResultsFinalizedEventType = "SessionResultsFinalized";

    private SessionEvent()
    {
        SourceEventKey = string.Empty;
        EventType = string.Empty;
        PayloadSummary = string.Empty;
    }

    private SessionEvent(
        string sourceEventKey,
        Guid liveSessionId,
        string eventType,
        DateTimeOffset occurredAt,
        string payloadSummary,
        int? responsibleUserId)
    {
        SessionEventId = Guid.NewGuid();
        SourceEventKey = sourceEventKey;
        LiveSessionId = liveSessionId;
        EventType = eventType;
        OccurredAt = occurredAt;
        PayloadSummary = payloadSummary;
        ResponsibleUserId = responsibleUserId;
    }

    public Guid SessionEventId { get; private set; }

    public string SourceEventKey { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public string EventType { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string PayloadSummary { get; private set; }

    public int? ResponsibleUserId { get; private set; }

    public static SessionEvent ForStateChange(
        Guid liveSessionId,
        SessionState previousState,
        SessionState currentState,
        DateTimeOffset changedAt,
        int? responsibleUserId,
        string? reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var transition = $"{previousState}→{currentState}";
        var summary = normalizedReason is null ? transition : $"{transition}: {normalizedReason}";

        return new SessionEvent(
            BuildSourceEventKey(StateChangedEventType, liveSessionId, changedAt, $"{previousState}:{currentState}"),
            liveSessionId,
            StateChangedEventType,
            changedAt,
            summary,
            responsibleUserId);
    }

    public static SessionEvent ForQuestionClosed(Guid liveSessionId, int questionIndex, DateTimeOffset closedAt)
    {
        if (questionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionIndex), "Question index cannot be negative.");
        }

        return new SessionEvent(
            BuildSourceEventKey(QuestionClosedEventType, liveSessionId, closedAt, questionIndex.ToString(CultureInfo.InvariantCulture)),
            liveSessionId,
            QuestionClosedEventType,
            closedAt,
            $"Question {questionIndex} closed",
            null);
    }

    public static SessionEvent ForResultsFinalized(Guid liveSessionId, DateTimeOffset finishedAt)
    {
        return new SessionEvent(
            BuildSourceEventKey(ResultsFinalizedEventType, liveSessionId, finishedAt, null),
            liveSessionId,
            ResultsFinalizedEventType,
            finishedAt,
            "Session results finalized",
            null);
    }

    private static string BuildSourceEventKey(
        string eventType,
        Guid liveSessionId,
        DateTimeOffset occurredAt,
        string? discriminator)
    {
        var prefix = $"{eventType}:{liveSessionId:D}:{occurredAt:O}";
        return discriminator is null ? prefix : $"{prefix}:{discriminator}";
    }
}
