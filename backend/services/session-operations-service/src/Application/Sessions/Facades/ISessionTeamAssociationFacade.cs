using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;

namespace umbral_backend.Application.Sessions.Facades;

public interface ISessionTeamAssociationFacade
{
    Task<AssociateTeamToSessionResultDto> AssociateAsync(
        AssociateTeamToSessionCommand command,
        CancellationToken cancellationToken);

    Task<AssociateTeamToSessionResultDto> AssociateByCodeAsync(
        AssociateTeamToSessionByCodeCommand command,
        CancellationToken cancellationToken);

    Task<SessionAssociatedTeamsDto> GetAssociatedTeamsAsync(
        GetAssociatedTeamsForSessionQuery query,
        CancellationToken cancellationToken);

    Task<SessionAssociatedTeamsDto> GetAssociatedTeamsByCodeAsync(
        GetAssociatedTeamsForSessionByCodeQuery query,
        CancellationToken cancellationToken);
}
