using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class GetAssociatedTeamsForSessionQueryHandler
    : IRequestHandler<GetAssociatedTeamsForSessionQuery, SessionAssociatedTeamsDto>
{
    private readonly ISessionTeamAssociationFacade _facade;

    public GetAssociatedTeamsForSessionQueryHandler(ISessionTeamAssociationFacade facade)
    {
        _facade = facade;
    }

    public Task<SessionAssociatedTeamsDto> Handle(
        GetAssociatedTeamsForSessionQuery request,
        CancellationToken cancellationToken)
    {
        return _facade.GetAssociatedTeamsAsync(request, cancellationToken);
    }
}
