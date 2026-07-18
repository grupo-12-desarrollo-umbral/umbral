using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Application.Users.Commands.AssignUserRole;

namespace umbral_backend.Application.Users.Common.Authorization;

public sealed class UserRoleAssignmentAuthorizationProxy : IRequestHandler<AssignUserRoleCommand>
{
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly AssignUserRoleCommandHandler _inner;

    public UserRoleAssignmentAuthorizationProxy(
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        AssignUserRoleCommandHandler inner)
    {
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _inner = inner;
    }

    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.AdministratorPanel);

        await _inner.Handle(request, cancellationToken);
    }
}
