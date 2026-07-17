using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.SessionEvents;
using umbral_backend.Application.Scores.Common.Authorization;
using umbral_backend.Application.SessionEvents.Common;

namespace umbral_backend.Application.SessionEvents.Queries.GetSessionHistory;

public sealed class GetSessionHistoryQueryHandler : IRequestHandler<GetSessionHistoryQuery, SessionHistoryDto>
{
    private readonly ISessionEventHistoryRepository _historyRepository;
    private readonly IScoringSessionAccessResolver _accessResolver;

    public GetSessionHistoryQueryHandler(
        ISessionEventHistoryRepository historyRepository,
        IScoringSessionAccessResolver accessResolver)
    {
        _historyRepository = historyRepository;
        _accessResolver = accessResolver;
    }

    public async Task<SessionHistoryDto> Handle(
        GetSessionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        await _accessResolver.EnsureAccessAsync(request.LiveSessionId, cancellationToken);

        var events = await _historyRepository.GetBySessionAsync(
            request.LiveSessionId,
            request.TeamId,
            cancellationToken);

        return events.Count == 0
            ? SessionHistoryDto.Empty(request.LiveSessionId)
            : SessionHistoryDtoFactory.Create(request.LiveSessionId, events);
    }
}
