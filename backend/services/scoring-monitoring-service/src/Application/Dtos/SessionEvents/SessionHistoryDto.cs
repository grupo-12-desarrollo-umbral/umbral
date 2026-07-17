namespace umbral_backend.Application.Dtos.SessionEvents;

public sealed record SessionHistoryDto(
    Guid LiveSessionId,
    IReadOnlyList<SessionHistoryRowDto> Events)
{
    public static SessionHistoryDto Empty(Guid liveSessionId)
    {
        return new SessionHistoryDto(liveSessionId, Array.Empty<SessionHistoryRowDto>());
    }
}

public sealed record SessionHistoryRowDto(
    Guid SessionEventId,
    string EventType,
    Guid? TeamId,
    DateTimeOffset OccurredAt,
    Guid? ResponsibleUserExternalId,
    string PayloadSummary);
