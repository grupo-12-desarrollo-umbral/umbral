using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.TransitionSessionState;

public interface ITransitionSessionStateFacade
{
    Task<TransitionSessionStateResultDto> TransitionAsync(
        TransitionSessionStateCommand command,
        CancellationToken cancellationToken);
}
