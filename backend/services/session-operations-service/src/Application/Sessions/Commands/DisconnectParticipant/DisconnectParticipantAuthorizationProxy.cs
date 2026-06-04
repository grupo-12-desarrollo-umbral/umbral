using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

public sealed class DisconnectParticipantAuthorizationProxy : IDisconnectParticipantService
{
    private readonly ICurrentUser _currentUser;
    private readonly IDisconnectParticipantExecutor _inner;

    public DisconnectParticipantAuthorizationProxy(
        ICurrentUser currentUser,
        IDisconnectParticipantExecutor inner)
    {
        _currentUser = currentUser;
        _inner = inner;
    }

    public Task DisconnectAsync(
        DisconnectParticipantCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        return _inner.DisconnectAsync(command, cancellationToken);
    }
}
