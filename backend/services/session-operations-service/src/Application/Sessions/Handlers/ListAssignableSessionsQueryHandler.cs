using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class ListAssignableSessionsQueryHandler
    : IRequestHandler<ListAssignableSessionsQuery, IReadOnlyList<SessionOperatorSummaryDto>>
{
    private readonly ILiveSessionRepository _repository;

    public ListAssignableSessionsQueryHandler(ILiveSessionRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<SessionOperatorSummaryDto>> Handle(
        ListAssignableSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return _repository.ListAssignableSummariesAsync(cancellationToken);
    }
}
