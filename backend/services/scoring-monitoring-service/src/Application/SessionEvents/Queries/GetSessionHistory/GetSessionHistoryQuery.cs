using umbral_backend.Application.Dtos.SessionEvents;

namespace umbral_backend.Application.SessionEvents.Queries.GetSessionHistory;

public sealed record GetSessionHistoryQuery(Guid LiveSessionId, Guid? TeamId) : IRequest<SessionHistoryDto>;
