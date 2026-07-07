using FluentValidation.Results;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.AssociateTeamToSession;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.Queries.GetAssociatedTeamsForSession;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Application.Sessions.Common;

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

    public Task<AssociateTeamToSessionResultDto> AssociateAsync(
        AssociateTeamToSessionCommand command,
        CancellationToken cancellationToken)
    {
        return AssociateCoreAsync(
            command.ReferenceTeamId,
            command.LiveSessionId,
            ct => _liveSessionRepository.GetByIdAsync(command.LiveSessionId, ct),
            cancellationToken);
    }

    public Task<AssociateTeamToSessionResultDto> AssociateByCodeAsync(
        AssociateTeamToSessionByCodeCommand command,
        CancellationToken cancellationToken)
    {
        return AssociateCoreAsync(
            command.ReferenceTeamId,
            command.SessionCode,
            ct => _liveSessionRepository.GetBySessionCodeAsync(command.SessionCode, ct),
            cancellationToken);
    }

    public async Task<SessionAssociatedTeamsDto> GetAssociatedTeamsAsync(
        GetAssociatedTeamsForSessionQuery query,
        CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetByIdAsync(query.LiveSessionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), query.LiveSessionId);

        return BuildAssociatedTeams(liveSession);
    }

    public async Task<SessionAssociatedTeamsDto> GetAssociatedTeamsByCodeAsync(
        GetAssociatedTeamsForSessionByCodeQuery query,
        CancellationToken cancellationToken)
    {
        var liveSession = await _liveSessionRepository.GetBySessionCodeAsync(query.SessionCode, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), query.SessionCode);

        return BuildAssociatedTeams(liveSession);
    }

    private async Task<AssociateTeamToSessionResultDto> AssociateCoreAsync(
        Guid referenceTeamId,
        object sessionKey,
        Func<CancellationToken, Task<LiveSession?>> loadSession,
        CancellationToken cancellationToken)
    {
        var teamReference = await _teamReferenceCatalogClient.GetByIdAsync(referenceTeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(TeamReferenceDto), referenceTeamId);

        EnsureTeamIsActive(teamReference);

        var liveSession = await loadSession(cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), sessionKey);

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

    private static SessionAssociatedTeamsDto BuildAssociatedTeams(LiveSession liveSession)
    {
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
