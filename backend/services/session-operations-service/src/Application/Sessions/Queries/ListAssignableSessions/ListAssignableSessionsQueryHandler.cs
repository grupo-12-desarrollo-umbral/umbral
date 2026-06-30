using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

namespace umbral_backend.Application.Sessions.Queries.ListAssignableSessions;

public sealed class ListAssignableSessionsQueryHandler
    : IRequestHandler<ListAssignableSessionsQuery, IReadOnlyList<SessionOperatorSummaryDto>>
{
    private readonly ILiveSessionRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthenticatedActorProfileAccessClient _authenticatedActorProfileAccessClient;

    public ListAssignableSessionsQueryHandler(
        ILiveSessionRepository repository,
        ICurrentUser currentUser,
        IAuthenticatedActorProfileAccessClient authenticatedActorProfileAccessClient)
    {
        _repository = repository;
        _currentUser = currentUser;
        _authenticatedActorProfileAccessClient = authenticatedActorProfileAccessClient;
    }

    public async Task<IReadOnlyList<SessionOperatorSummaryDto>> Handle(
        ListAssignableSessionsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        if (string.Equals(_currentUser.Role, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return await _repository.ListAssignableSummariesAsync(
                assignedOperatorUserId: null,
                includeConcluded: false,
                cancellationToken);
        }

        if (!string.Equals(_currentUser.Role, "Operator", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }

        var actor = await _authenticatedActorProfileAccessClient.GetCurrentAsync(cancellationToken);

        return await _repository.ListAssignableSummariesAsync(
            actor.UserId,
            includeConcluded: true,
            cancellationToken);
    }
}
