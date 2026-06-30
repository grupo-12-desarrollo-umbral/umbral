using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;

public sealed class AssociateTeamToSessionByCodeCommandHandler
    : IRequestHandler<AssociateTeamToSessionByCodeCommand, AssociateTeamToSessionResultDto>
{
    private readonly ISessionTeamAssociationFacade _facade;

    public AssociateTeamToSessionByCodeCommandHandler(ISessionTeamAssociationFacade facade)
    {
        _facade = facade;
    }

    public Task<AssociateTeamToSessionResultDto> Handle(
        AssociateTeamToSessionByCodeCommand request,
        CancellationToken cancellationToken)
    {
        return _facade.AssociateByCodeAsync(request, cancellationToken);
    }
}
