using umbral_backend.Application.Dtos.SessionEvents;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.SessionEvents.Common;

internal static class SessionHistoryDtoFactory
{
    public static SessionHistoryDto Create(Guid liveSessionId, IReadOnlyList<SessionEvent> events)
    {
        return new SessionHistoryDto(
            liveSessionId,
            events
                .OrderBy(sessionEvent => sessionEvent.OccurredAt)
                .ThenBy(sessionEvent => sessionEvent.SessionEventId)
                .Select(sessionEvent => new SessionHistoryRowDto(
                    sessionEvent.SessionEventId,
                    sessionEvent.EventType,
                    sessionEvent.TeamId,
                    sessionEvent.OccurredAt,
                    sessionEvent.ResponsibleUserExternalId,
                    sessionEvent.PayloadSummary))
                .ToArray());
    }
}
