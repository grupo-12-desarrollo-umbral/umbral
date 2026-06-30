using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public sealed class ParticipantMembershipAccessAuthorizationProxy
    : IRequestHandler<ValidateParticipantMembershipAccessQuery, ParticipantMembershipAccessDecisionDto>
{
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly ValidateParticipantMembershipAccessQueryHandler _inner;

    public ParticipantMembershipAccessAuthorizationProxy(
        ITeamRepository teamRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        ValidateParticipantMembershipAccessQueryHandler inner)
    {
        _teamRepository = teamRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<ParticipantMembershipAccessDecisionDto> Handle(
        ValidateParticipantMembershipAccessQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.ParticipantExperience);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(request.TeamId, cancellationToken);
        var belongsToTeam = team is not null
            && team.IsActive
            && team.Memberships.Any(membership => membership.UserId == actor.Id);

        if (!belongsToTeam)
        {
            throw new ForbiddenAccessException();
        }

        return await _inner.Handle(request, cancellationToken);
    }
}
