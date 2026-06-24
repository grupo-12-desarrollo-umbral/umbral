using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;

public interface IReconnectAuthenticatedParticipantExecutor
{
    Task<ReconnectParticipantResultDto> ReconnectAsync(
        ReconnectAuthenticatedParticipantCommand command,
        CancellationToken cancellationToken);
}
