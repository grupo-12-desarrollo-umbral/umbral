using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
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

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.AdministratorPanel);

        await _inner.AssignAsync(command, cancellationToken);
    }
}
