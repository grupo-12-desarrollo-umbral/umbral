using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public interface IReconnectAuthenticatedParticipantExecutor
{
    Task<ReconnectParticipantResultDto> ReconnectAsync(
        ReconnectAuthenticatedParticipantCommand command,
        CancellationToken cancellationToken);
}
