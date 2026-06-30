using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

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
