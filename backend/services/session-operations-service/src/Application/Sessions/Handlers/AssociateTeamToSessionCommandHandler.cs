using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class AssociateTeamToSessionCommandHandler
    : IRequestHandler<AssociateTeamToSessionCommand, AssociateTeamToSessionResultDto>
{
    private readonly ISessionTeamAssociationFacade _facade;

    public AssociateTeamToSessionCommandHandler(ISessionTeamAssociationFacade facade)
    {
        _facade = facade;
    }

    public Task<AssociateTeamToSessionResultDto> Handle(
        AssociateTeamToSessionCommand request,
        CancellationToken cancellationToken)
    {
        return _facade.AssociateAsync(request, cancellationToken);
    }
}
