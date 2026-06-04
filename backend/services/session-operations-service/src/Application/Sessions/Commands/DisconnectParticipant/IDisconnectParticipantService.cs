namespace umbral_backend.Application.Sessions.Commands.DisconnectParticipant;

public interface IDisconnectParticipantService
{
    Task DisconnectAsync(
        DisconnectParticipantCommand command,
        CancellationToken cancellationToken);
}
