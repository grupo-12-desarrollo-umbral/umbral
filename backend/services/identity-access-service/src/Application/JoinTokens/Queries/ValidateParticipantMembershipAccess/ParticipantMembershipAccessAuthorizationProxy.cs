using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.JoinTokens.Queries.ValidateParticipantMembershipAccess;

public sealed class ParticipantMembershipAccessAuthorizationProxy : IValidateParticipantMembershipAccessService
{
    private readonly IUserRepository _userRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;
    private readonly IValidateParticipantMembershipAccessExecutor _inner;

    public ParticipantMembershipAccessAuthorizationProxy(
        IUserRepository userRepository,
        ITeamRepository teamRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy,
        IValidateParticipantMembershipAccessExecutor inner)
    {
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task<ParticipantMembershipAccessDecisionDto> ValidateAsync(
        ValidateParticipantMembershipAccessQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.ParticipantExperience);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(query.TeamId, cancellationToken);
        var belongsToTeam = team is not null
            && team.IsActive
            && team.Memberships.Any(membership => membership.UserId == actor.Id);

        if (!belongsToTeam)
        {
            throw new ForbiddenAccessException();
        }

        return await _inner.ValidateAsync(query, cancellationToken);
    }
}
