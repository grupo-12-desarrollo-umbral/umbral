using umbral_backend.Application.Sessions.Commands.CreateTriviaSession;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class CreateTriviaSessionCommandHandler
    : IRequestHandler<CreateTriviaSessionCommand, CreateTriviaSessionResultDto>
{
    private readonly ICreateTriviaSessionFacade _facade;

    public CreateTriviaSessionCommandHandler(ICreateTriviaSessionFacade facade)
    {
        _facade = facade;
    }

    public Task<CreateTriviaSessionResultDto> Handle(
        CreateTriviaSessionCommand request,
        CancellationToken cancellationToken)
    {
        return _facade.CreateAsync(request, cancellationToken);
    }
}
