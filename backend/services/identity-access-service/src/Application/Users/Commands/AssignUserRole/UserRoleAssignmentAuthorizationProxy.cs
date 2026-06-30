using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Commands.AssignUserRole;

public sealed class UserRoleAssignmentAuthorizationProxy : IUserRoleAssignmentService
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IUserRoleAssignmentService _inner;

    public UserRoleAssignmentAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        IUserRoleAssignmentService inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task AssignAsync(AssignUserRoleCommand command, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

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
