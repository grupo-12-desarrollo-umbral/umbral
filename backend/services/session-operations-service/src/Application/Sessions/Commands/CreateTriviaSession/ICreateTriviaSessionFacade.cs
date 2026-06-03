using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Commands.CreateTriviaSession;

public interface ICreateTriviaSessionFacade
{
    Task<CreateTriviaSessionResultDto> CreateAsync(CreateTriviaSessionCommand command, CancellationToken cancellationToken);
}
