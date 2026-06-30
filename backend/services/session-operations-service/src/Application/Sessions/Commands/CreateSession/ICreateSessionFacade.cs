using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.CreateSession;

public interface ICreateSessionFacade
{
    Task<CreateSessionResultDto> CreateAsync(CreateSessionCommand command, CancellationToken cancellationToken);
}
