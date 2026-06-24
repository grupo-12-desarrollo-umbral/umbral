using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.AssignParticipantToTeam;

public sealed class AssignParticipantToTeamCommandHandler : IRequestHandler<AssignParticipantToTeamCommand, Guid>
{
    private readonly ITeamRepository _teamRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;

    public AssignParticipantToTeamCommandHandler(
        ITeamRepository teamRepository,
        IUserRepository userRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy)
    {
        _teamRepository = teamRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
    }

    public async Task<Guid> Handle(AssignParticipantToTeamCommand request, CancellationToken cancellationToken)
    {
        var actor = await GetCurrentActorAsync(cancellationToken);
        EnsureActorCanManageTeams(actor);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(Team), request.TeamId);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        if (user.Role != Role.Participant)
        {
            throw new UserNotParticipantRoleException(user.Id, user.Role);
        }

        var membership = team.AssignParticipant(user.Id);

        await _teamRepository.UpdateAsync(team, cancellationToken);

        return membership.TeamMembershipId;
    }

    private async Task<User> GetCurrentActorAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        return await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);
    }

    private void EnsureActorCanManageTeams(User actor)
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
