using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Permissions.Queries.ValidateParticipantMembershipAccess;

public sealed class ParticipantMembershipAccessAuthorizationProxy
    : IRequestHandler<ValidateParticipantMembershipAccessQuery, ParticipantMembershipAccessDecisionDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly ValidateParticipantMembershipAccessQueryHandler _inner;

    public ParticipantMembershipAccessAuthorizationProxy(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        ValidateParticipantMembershipAccessQueryHandler inner)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _inner = inner;
    }

    public async Task<ParticipantMembershipAccessDecisionDto> Handle(
        ValidateParticipantMembershipAccessQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(request.TeamId, cancellationToken);

        if (!actor.IsActive)
        {
            return Deny(
                request,
                ParticipantMembershipAccessReasonCodes.UserAccessDeactivated,
                "User is deactivated in Users.");
        }

        if (actor.Role != Role.Participant)
        {
            return Deny(
                request,
                ParticipantMembershipAccessReasonCodes.UserNotParticipant,
                "User is not a participant.");
        }

        if (team is null)
        {
            return Deny(
                request,
                ParticipantMembershipAccessReasonCodes.RegisteredTeamNotFound,
                "Registered team was not found.");
        }

        if (!team.IsActive)
        {
            return Deny(
                request,
                ParticipantMembershipAccessReasonCodes.RegisteredTeamInactive,
                "Registered team is inactive.");
        }

        if (!team.Memberships.Any(membership => membership.UserId == actor.Id))
        {
            return Deny(
                request,
                ParticipantMembershipAccessReasonCodes.ParticipantNotAuthorizedForRegisteredTeam,
                "Participant is not authorized for the registered team.");
        }

        return await _inner.Handle(request, cancellationToken);
    }

    private static ParticipantMembershipAccessDecisionDto Deny(
        ValidateParticipantMembershipAccessQuery request,
        string reasonCode,
        string reason)
    {
        return new ParticipantMembershipAccessDecisionDto(
            nameof(ProtectedCapability.ParticipantExperience),
            false,
            reasonCode,
            reason,
            request.LiveSessionId,
            request.TeamId);
    }
}
