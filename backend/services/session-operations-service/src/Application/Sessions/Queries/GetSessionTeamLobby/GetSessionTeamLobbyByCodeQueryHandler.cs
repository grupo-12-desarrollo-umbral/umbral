using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Sessions.Queries.GetSessionTeamLobby;

// Participant team lobby read (#108). Projects the session's attached teams to per-participant join
// states, reusing the #88 Open Team Selection policy for the joinable set.
public sealed class GetSessionTeamLobbyByCodeQueryHandler
    : IRequestHandler<GetSessionTeamLobbyByCodeQuery, SessionTeamLobbyDto>
{
    private readonly ILiveSessionRepository _liveSessionRepository;
    private readonly IParticipantEligibleTeamsClient _eligibleTeamsClient;
    private readonly ICurrentUser _currentUser;
    private readonly OpenTeamSelectionPolicy _openTeamSelectionPolicy;

    public GetSessionTeamLobbyByCodeQueryHandler(
        ILiveSessionRepository liveSessionRepository,
        IParticipantEligibleTeamsClient eligibleTeamsClient,
        ICurrentUser currentUser,
        OpenTeamSelectionPolicy openTeamSelectionPolicy)
    {
        _liveSessionRepository = liveSessionRepository;
        _eligibleTeamsClient = eligibleTeamsClient;
        _currentUser = currentUser;
        _openTeamSelectionPolicy = openTeamSelectionPolicy;
    }

    public async Task<SessionTeamLobbyDto> Handle(
        GetSessionTeamLobbyByCodeQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _liveSessionRepository.GetBySessionCodeAsync(request.SessionCode, cancellationToken)
            ?? throw new NotFoundException(nameof(LiveSession), request.SessionCode);

        if (!Guid.TryParse(_currentUser.Id, out var externalIdentityId))
        {
            throw new UnauthorizedAccessException();
        }

        var whitelist = await _eligibleTeamsClient.GetAsync(cancellationToken);

        // Denied/deactivated participant (IsEligible=false) => nothing joinable. We must NOT hand an
        // empty authorized set to the policy here: it reads empty as "unassigned → Open Team
        // Selection (all attached joinable)", which is wrong for a denied user.
        var selectableIds = whitelist.IsEligible
            ? _openTeamSelectionPolicy
                .SelectableTeams(session, whitelist.Teams.Select(team => team.TeamId).ToHashSet())
                .Select(team => team.TeamId)
                .ToHashSet()
            : new HashSet<Guid>();

        var currentTeam = session.FindTeamForExternalParticipant(externalIdentityId);

        var teams = session.Teams
            .Select(team => new SessionTeamDto(
                team.TeamId,
                team.ReferenceTeamId,
                team.DisplayName,
                ResolveJoinState(team, currentTeam, selectableIds)))
            .ToList();

        return new SessionTeamLobbyDto(session.LiveSessionId, session.SessionCode, teams);
    }

    private static string ResolveJoinState(Team team, Team? currentTeam, IReadOnlySet<Guid> selectableIds)
    {
        if (currentTeam is not null && team.TeamId == currentTeam.TeamId)
        {
            return "mine";
        }

        if (selectableIds.Contains(team.TeamId) && team.ActiveMemberCount < team.Capacity)
        {
            return "joinable";
        }

        return "locked";
    }
}
