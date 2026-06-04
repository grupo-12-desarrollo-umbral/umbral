using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public sealed class SessionAdministrationAuthorizationProxy : ISessionAdministrationAccessResolver
{
    private const string AdministratorRole = "Administrator";
    private const string OperatorRole = "Operator";

    private readonly ICurrentUser _currentUser;
    private readonly ISessionAdministrationAccessExecutor _inner;
    private readonly IAuthenticatedActorProfileAccessClient _authenticatedActorProfileAccessClient;

    public SessionAdministrationAuthorizationProxy(
        ICurrentUser currentUser,
        ISessionAdministrationAccessExecutor inner,
        IAuthenticatedActorProfileAccessClient authenticatedActorProfileAccessClient)
    {
        _currentUser = currentUser;
        _inner = inner;
        _authenticatedActorProfileAccessClient = authenticatedActorProfileAccessClient;
    }

    public async Task<LiveSession> GetAuthorizedSessionAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var liveSession = await GetAuthorizedSessionInternalAsync(
            liveSessionId,
            cancellationToken,
            _inner.GetAuthorizedSessionAsync);

        return liveSession;
    }

    public async Task<LiveSession> GetAuthorizedTimerSessionAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var liveSession = await GetAuthorizedSessionInternalAsync(
            liveSessionId,
            cancellationToken,
            _inner.GetAuthorizedTimerSessionAsync);

        return liveSession;
    }

    private async Task<LiveSession> GetAuthorizedSessionInternalAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken,
        Func<Guid, CancellationToken, Task<LiveSession>> loadSessionAsync)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var liveSession = await loadSessionAsync(liveSessionId, cancellationToken);

        if (string.Equals(_currentUser.Role, AdministratorRole, StringComparison.OrdinalIgnoreCase))
        {
            return liveSession;
        }

        if (!string.Equals(_currentUser.Role, OperatorRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }

        var actor = await _authenticatedActorProfileAccessClient.GetCurrentAsync(cancellationToken);

        if (liveSession.AssignedOperatorUserId != actor.UserId)
        {
            throw new ForbiddenAccessException();
        }

        return liveSession;
    }
}
