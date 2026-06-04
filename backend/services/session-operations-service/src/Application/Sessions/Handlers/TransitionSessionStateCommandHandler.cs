using umbral_backend.Application.Sessions.Commands.TransitionSessionState;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class TransitionSessionStateCommandHandler
    : IRequestHandler<TransitionSessionStateCommand, TransitionSessionStateResultDto>
{
    private readonly ITransitionSessionStateFacade _facade;

    public TransitionSessionStateCommandHandler(ITransitionSessionStateFacade facade)
    {
        _facade = facade;
    }

    public Task<TransitionSessionStateResultDto> Handle(
        TransitionSessionStateCommand request,
        CancellationToken cancellationToken)
    {
        return _facade.TransitionAsync(request, cancellationToken);
    }
}
