namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

public interface IDisconnectParticipantExecutor
{
    Task DisconnectAsync(
        DisconnectParticipantCommand command,
        CancellationToken cancellationToken);
}
