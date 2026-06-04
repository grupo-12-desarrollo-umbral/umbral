using FluentValidation.Results;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Facades;

public sealed class SessionTeamAssociationFacade : ISessionTeamAssociationFacade
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly ITeamReferenceCatalogClient _teamReferenceCatalogClient;

    public SessionTeamAssociationFacade(
        ILiveSessionRepository liveSessionRepository,
        ITeamReferenceCatalogClient teamReferenceCatalogClient)
    {
        _liveSessionRepository = liveSessionRepository;
        _teamReferenceCatalogClient = teamReferenceCatalogClient;
    }

    public async Task<AssociateTeamToSessionResultDto> AssociateAsync(
        AssociateTeamToSessionCommand command,
        CancellationToken cancellationToken)
    {
        var teamReference = await _teamReferenceCatalogClient.GetByIdAsync(command.ReferenceTeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(TeamReferenceDto), command.ReferenceTeamId);

        EnsureTeamIsActive(teamReference);

        var liveSession = await _liveSessionRepository.GetByIdAsync(command.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), command.LiveSessionId);

        var team = liveSession.AssociateTeam(
            teamReference.TeamId,
            teamReference.DisplayName,
            teamReference.TeamCode,
            Math.Max(1, teamReference.ParticipantCount));

        await _liveSessionRepository.UpdateAsync(liveSession, cancellationToken);

        return new AssociateTeamToSessionResultDto(
            liveSession.LiveSessionId,
            team.TeamId,
            team.ReferenceTeamId ?? teamReference.TeamId,
            team.DisplayName,
            team.TeamCode.Value,
            liveSession.State.ToString(),
            liveSession.AssociatedTeamCount);
    }

    public async Task<SessionAssociatedTeamsDto> GetAssociatedTeamsAsync(
        GetAssociatedTeamsForSessionQuery query,
        CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(query.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), query.LiveSessionId);

        var teams = liveSession.Teams
            .Where(team => team.ReferenceTeamId.HasValue)
            .Select(team => new AssociatedSessionTeamDto(
                team.TeamId,
                team.ReferenceTeamId!.Value,
                team.DisplayName,
                team.TeamCode.Value,
                team.JoinStatus.ToString()))
            .ToList();

        return new SessionAssociatedTeamsDto(liveSession.LiveSessionId, teams);
    }

    private static void EnsureTeamIsActive(TeamReferenceDto teamReference)
    {
        if (teamReference.IsActive)
        {
            return;
        }

        throw new umbral_backend.Application.Common.Exceptions.ValidationException(
            new[]
            {
                new ValidationFailure(
                    nameof(AssociateTeamToSessionCommand.ReferenceTeamId),
                    $"Team reference '{teamReference.TeamId}' is inactive and cannot be associated to a session.")
            });
    }
}
