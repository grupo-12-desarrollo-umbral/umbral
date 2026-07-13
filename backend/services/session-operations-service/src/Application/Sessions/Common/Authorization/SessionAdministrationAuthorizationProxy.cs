using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common.Authorization;

public sealed class SessionAdministrationAuthorizationProxy : ISessionAdministrationAccessResolver
{
    private const string AdministratorRole = "Administrator";
    private const string OperatorRole = "Operator";

    private readonly ICurrentUser _currentUser;
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IAuthenticatedActorProfileAccessClient _authenticatedActorProfileAccessClient;

    public SessionAdministrationAuthorizationProxy(
        ICurrentUser currentUser,
        ILiveSessionRepository liveSessionRepository,
        IAuthenticatedActorProfileAccessClient authenticatedActorProfileAccessClient)
    {
        _currentUser = currentUser;
        _liveSessionRepository = liveSessionRepository;
        _authenticatedActorProfileAccessClient = authenticatedActorProfileAccessClient;
    }

    public async Task<LiveSession> GetAuthorizedSessionAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
        => (await GetAuthorizedSessionInternalAsync(
            liveSessionId,
            cancellationToken,
            LoadSessionAsync)).Session;

    public Task<(LiveSession Session, int? ResponsibleUserId)> GetAuthorizedSessionWithActorAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
        => GetAuthorizedSessionInternalAsync(liveSessionId, cancellationToken, LoadSessionAsync);

    public async Task<LiveSession> GetAuthorizedTimerSessionAsync(
        Guid liveSessionId,
        CancellationToken cancellationToken)
        => (await GetAuthorizedSessionInternalAsync(
            liveSessionId,
            cancellationToken,
            LoadTimerSessionAsync)).Session;

    private async Task<LiveSession> LoadSessionAsync(Guid liveSessionId, CancellationToken cancellationToken)
        => await _liveSessionRepository.GetByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);

    private async Task<LiveSession> LoadTimerSessionAsync(Guid liveSessionId, CancellationToken cancellationToken)
        => await _liveSessionRepository.GetTimerSessionByIdAsync(liveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), liveSessionId);

    private async Task<(LiveSession Session, int? ResponsibleUserId)> GetAuthorizedSessionInternalAsync(
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
            return (liveSession, null);
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

        return (liveSession, actor.UserId);
    }
}
