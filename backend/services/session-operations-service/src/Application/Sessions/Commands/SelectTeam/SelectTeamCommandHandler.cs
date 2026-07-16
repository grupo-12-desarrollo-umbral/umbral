using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Commands.SelectTeam;

public sealed class SelectTeamCommandHandler
    : IRequestHandler<SelectTeamCommand, SelectTeamResultDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IParticipantEligibleTeamsClient _eligibleTeamsClient;
    private readonly ICurrentUser _currentUser;
    private readonly OpenTeamSelectionPolicy _openTeamSelectionPolicy;
    private readonly TimeProvider _timeProvider;

    public SelectTeamCommandHandler(
        ILiveSessionRepository liveSessionRepository,
        IParticipantEligibleTeamsClient eligibleTeamsClient,
        ICurrentUser currentUser,
        OpenTeamSelectionPolicy openTeamSelectionPolicy,
        TimeProvider timeProvider)
    {
        _liveSessionRepository = liveSessionRepository;
        _eligibleTeamsClient = eligibleTeamsClient;
        _currentUser = currentUser;
        _openTeamSelectionPolicy = openTeamSelectionPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<SelectTeamResultDto> Handle(
        SelectTeamCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _liveSessionRepository.GetBySessionCodeAsync(request.SessionCode, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.SessionCode);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            throw new UnauthorizedAccessException();
        }

        var whitelist = await _eligibleTeamsClient.GetAsync(cancellationToken);
        if (!whitelist.IsEligible)
        {
            throw new ForbiddenAccessException();
        }

        // Reference/catalog team ids; empty => unassigned participant, domain treats it as "all attached selectable".
        var authorizedReferenceTeamIds = whitelist.Teams.Select(team => team.TeamId).ToHashSet();

        var occurredAt = _timeProvider.GetUtcNow();
        var (participant, team) = session.SelectTeam(
            externalIdentityId,
            _currentUser.DisplayName,
            request.RuntimeTeamId,
            authorizedReferenceTeamIds,
            occurredAt,
            _openTeamSelectionPolicy);

        await _liveSessionRepository.UpdateAsync(session, cancellationToken);

        var membership = team.Members.Single(member =>
            member.IsActive && member.SessionParticipantId == participant.SessionParticipantId);

        return new SelectTeamResultDto(
            membership.TeamMemberId,
            session.LiveSessionId,
            team.TeamId,
            participant.SessionParticipantId,
            session.State.ToString());
    }
}
