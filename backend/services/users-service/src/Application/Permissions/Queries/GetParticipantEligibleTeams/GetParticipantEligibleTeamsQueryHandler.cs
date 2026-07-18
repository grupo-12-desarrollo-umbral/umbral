using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Permissions;
using umbral_backend.Application.Permissions.Common;
using umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Permissions.Queries.GetParticipantEligibleTeams;

// Answers "which RegisteredTeams is this participant generally eligible for" — the set-query half
// of the issue #90 query split. Same eligibility rules as the per-join point check
// (ParticipantMembershipAccessAuthorizationProxy), just returning the whole active whitelist instead
// of one decision. The global gate (deactivated / not a participant) is reason-coded once; an empty
// Teams list is otherwise "whitelisted for nothing", which is distinct from a denied user.
public sealed class GetParticipantEligibleTeamsQueryHandler
    : IRequestHandler<GetParticipantEligibleTeamsQuery, ParticipantEligibleTeamsDto>
{
    private readonly ICurrentActor _currentActor;
    private readonly ITeamRepository _teamRepository;

    public GetParticipantEligibleTeamsQueryHandler(ICurrentActor currentActor, ITeamRepository teamRepository)
    {
        _currentActor = currentActor;
        _teamRepository = teamRepository;
    }

    public async Task<ParticipantEligibleTeamsDto> Handle(
        GetParticipantEligibleTeamsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        if (!actor.IsActive)
        {
            return Denied(ParticipantMembershipAccessReasonCodes.UserAccessDeactivated);
        }

        if (actor.Role != Role.Participant)
        {
            return Denied(ParticipantMembershipAccessReasonCodes.UserNotParticipant);
        }

        var teams = await _teamRepository.ListActiveByParticipantAsync(actor.Id, cancellationToken);

        return new ParticipantEligibleTeamsDto(
            true,
            ParticipantMembershipAccessReasonCodes.Eligible,
            teams.Select(team => new EligibleTeamDto(team.TeamId, team.DisplayName, team.TeamCode)).ToArray());
    }

    private static ParticipantEligibleTeamsDto Denied(string reasonCode)
        => new(false, reasonCode, Array.Empty<EligibleTeamDto>());
}
