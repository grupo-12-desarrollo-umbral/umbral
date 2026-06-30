using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Queries.GetTeamParticipants;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Queries.GetTeamParticipants;

public sealed class GetTeamParticipantsQueryHandler : IRequestHandler<GetTeamParticipantsQuery, IReadOnlyList<TeamMembershipDto>>
{
    private readonly ITeamRepository _teamRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public GetTeamParticipantsQueryHandler(
        ITeamRepository teamRepository,
        IUserRepository userRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
    }

    public async Task<IReadOnlyList<TeamMembershipDto>> Handle(GetTeamParticipantsQuery request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        EnsureActorCanReadTeams(actor);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), request.TeamId);

        var memberships = new List<TeamMembershipDto>();
        foreach (var membership in team.Memberships)
        {
            var user = await _userRepository.GetByIdAsync(membership.UserId, cancellationToken);
            memberships.Add(new TeamMembershipDto(
                membership.TeamMembershipId,
                membership.TeamId,
                membership.UserId,
                user?.Email ?? "",
                user?.DisplayName ?? "",
                membership.AssignedAt));
        }

        return memberships.AsReadOnly();
    }

    private void EnsureActorCanReadTeams(User actor)
    {
        var decision = _accessPolicy.Evaluate(actor, ProtectedCapability.OperatorPanel);

        if (!actor.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(actor.Id);
        }

        if (!decision.IsAllowed)
        {
            throw new ForbiddenAccessException();
        }
    }
}
