using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Facades;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.Sessions.Handlers;

public sealed class GetAssociatedTeamsForSessionByCodeQueryHandler
    : IRequestHandler<GetAssociatedTeamsForSessionByCodeQuery, SessionAssociatedTeamsDto>
{
    private readonly ISessionTeamAssociationFacade _facade;

    public GetAssociatedTeamsForSessionByCodeQueryHandler(ISessionTeamAssociationFacade facade)
    {
        _facade = facade;
    }

    public Task<SessionAssociatedTeamsDto> Handle(
        GetAssociatedTeamsForSessionByCodeQuery request,
        CancellationToken cancellationToken)
    {
        return _facade.GetAssociatedTeamsByCodeAsync(request, cancellationToken);
    }
}
