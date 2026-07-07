using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Teams.Commands.AuthorizeParticipantForTeam;

public sealed class AuthorizeParticipantForTeamCommandHandler : IRequestHandler<AuthorizeParticipantForTeamCommand, Guid>
{
    private readonly ITeamRepository _teamRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;

    public AuthorizeParticipantForTeamCommandHandler(
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

    public async Task<Guid> Handle(AuthorizeParticipantForTeamCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);
        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.OperatorPanel);

        var team = await _teamRepository.GetByIdWithMembershipsAsync(request.TeamId, cancellationToken)
            ?? throw new NotFoundException(nameof(RegisteredTeam), request.TeamId);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        if (user.Role != Role.Participant)
        {
            throw new UserNotParticipantRoleException(user.Id, user.Role);
        }

        var membership = team.AuthorizeParticipant(user.Id);

        await _teamRepository.UpdateAsync(team, cancellationToken);

        return membership.TeamMembershipId;
    }
}
