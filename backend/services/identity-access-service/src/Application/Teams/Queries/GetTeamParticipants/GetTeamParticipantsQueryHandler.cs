using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Queries.GetTeamParticipants;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Queries.GetTeamParticipants;

public sealed class GetTeamParticipantsQueryHandler : IRequestHandler<GetTeamParticipantsQuery, IReadOnlyList<RegisteredTeamMembershipDto>>
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

    public async Task<IReadOnlyList<RegisteredTeamMembershipDto>> Handle(GetTeamParticipantsQuery request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegisteredTeam), request.TeamId);

        var memberships = new List<RegisteredTeamMembershipDto>();
        foreach (var membership in team.Memberships)
        {
            var user = await _userRepository.GetByIdAsync(membership.UserId, cancellationToken);
            memberships.Add(new RegisteredTeamMembershipDto(
                membership.TeamMembershipId,
                membership.TeamId,
                membership.UserId,
                user?.Email ?? "",
                user?.DisplayName ?? ""));
        }

        return memberships.AsReadOnly();
    }
}
