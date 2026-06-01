using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Commands.AssignUserRole;

public sealed class UserRoleAssignmentAuthorizationProxy : IUserRoleAssignmentService
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly AccessPolicy _accessPolicy;
    private readonly IUserRoleAssignmentExecutor _inner;

    public UserRoleAssignmentAuthorizationProxy(
        IUserRepository userRepository,
        ICurrentUser currentUser,
        AccessPolicy accessPolicy,
        IUserRoleAssignmentExecutor inner)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task AssignAsync(AssignUserRoleCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Id))
        {
            throw new UnauthorizedAccessException();
        }

        var actor = await _userRepository.GetByExternalIdentityIdAsync(_currentUser.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), _currentUser.Id);

        EnsureActorCanAssignRole(actor);

        await _inner.AssignAsync(command, cancellationToken);
    }

    private void EnsureActorCanAssignRole(User actor)
    {
        var decision = _accessPolicy.Evaluate(actor, ProtectedCapability.AdministratorPanel);

        if (!actor.IsActive)
        {
            throw new DeactivatedUserAccessDeniedException(actor.Id);
        }

        if (!decision.IsAllowed)
        {
            throw new UserRoleNotAuthorizedException(actor.Role, ProtectedCapability.AdministratorPanel);
        }
    }
}
