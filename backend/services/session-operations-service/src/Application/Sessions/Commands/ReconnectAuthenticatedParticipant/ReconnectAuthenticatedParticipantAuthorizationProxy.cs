using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public sealed class ReconnectAuthenticatedParticipantAuthorizationProxy : IReconnectAuthenticatedParticipantService
{
    private readonly ICurrentUser _currentUser;
    private readonly IReconnectAuthenticatedParticipantExecutor _inner;

    public ReconnectAuthenticatedParticipantAuthorizationProxy(
        ICurrentUser currentUser,
        IReconnectAuthenticatedParticipantExecutor inner)
    {
        _currentUser = currentUser;
        _inner = inner;
    }

    public Task<ReconnectParticipantResultDto> ReconnectAsync(
        ReconnectAuthenticatedParticipantCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        return _inner.ReconnectAsync(command, cancellationToken);
    }
}
