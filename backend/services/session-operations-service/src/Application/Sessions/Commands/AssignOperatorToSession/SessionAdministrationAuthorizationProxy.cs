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

    public SessionAdministrationAuthorizationProxy(
        ICurrentUser currentUser,
        ISessionAdministrationAccessExecutor inner)
    {
        _currentUser = currentUser;
        _inner = inner;
    }

    public async Task<LiveSession> GetAuthorizedSessionAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var liveSession = await _inner.GetAuthorizedSessionAsync(liveSessionId, cancellationToken);

        if (string.Equals(_currentUser.Role, AdministratorRole, StringComparison.OrdinalIgnoreCase))
        {
            return liveSession;
        }

        if (!string.Equals(_currentUser.Role, OperatorRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }

        if (!int.TryParse(_currentUser.Id, out var operatorUserId))
        {
            throw new UnauthorizedAccessException();
        }

        if (liveSession.AssignedOperatorUserId != operatorUserId)
        {
            throw new ForbiddenAccessException();
        }

        return liveSession;
    }
}
