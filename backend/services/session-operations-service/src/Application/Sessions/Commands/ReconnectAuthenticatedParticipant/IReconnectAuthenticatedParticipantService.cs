using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public interface IReconnectAuthenticatedParticipantService
{
    Task<ReconnectParticipantResultDto> ReconnectAsync(
        ReconnectAuthenticatedParticipantCommand command,
        CancellationToken cancellationToken);
}
