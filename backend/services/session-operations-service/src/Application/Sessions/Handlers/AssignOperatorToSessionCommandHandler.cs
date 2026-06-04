using umbral_backend.Application.Sessions.Commands.AssignOperatorToSession;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class AssignOperatorToSessionCommandHandler
    : IRequestHandler<AssignOperatorToSessionCommand, AssignOperatorToSessionResultDto>
{
    private readonly IAssignOperatorToSessionFacade _facade;

    public AssignOperatorToSessionCommandHandler(IAssignOperatorToSessionFacade facade)
    {
        _facade = facade;
    }

    public Task<AssignOperatorToSessionResultDto> Handle(
        AssignOperatorToSessionCommand request,
        CancellationToken cancellationToken)
    {
        return _facade.AssignAsync(request, cancellationToken);
    }
}
