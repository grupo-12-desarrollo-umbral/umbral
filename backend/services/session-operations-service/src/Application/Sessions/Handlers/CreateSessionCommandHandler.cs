using umbral_backend.Application.Sessions.Commands.CreateSession;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class CreateSessionCommandHandler
    : IRequestHandler<CreateSessionCommand, CreateSessionResultDto>
{
    private readonly ICreateSessionFacade _facade;

    public CreateSessionCommandHandler(ICreateSessionFacade facade)
    {
        _facade = facade;
    }

    public Task<CreateSessionResultDto> Handle(
        CreateSessionCommand request,
        CancellationToken cancellationToken)
    {
        return _facade.CreateAsync(request, cancellationToken);
    }
}
