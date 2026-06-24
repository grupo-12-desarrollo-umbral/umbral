using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;

public interface IAssignOperatorToSessionFacade
{
    Task<AssignOperatorToSessionResultDto> AssignAsync(
        AssignOperatorToSessionCommand command,
        CancellationToken cancellationToken);
}
