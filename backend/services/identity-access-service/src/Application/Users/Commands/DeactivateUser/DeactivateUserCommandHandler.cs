using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Users.Commands.DeactivateUser;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IIdentityProviderAdminService _identityProviderAdmin;

    public DeactivateUserCommandHandler(
        IUserRepository userRepository,
        ICurrentActor currentActor,
        AccessPolicy accessPolicy,
        IIdentityProviderAdminService identityProviderAdmin)
    {
        _userRepository = userRepository;
        _currentActor = currentActor;
        _accessPolicy = accessPolicy;
        _identityProviderAdmin = identityProviderAdmin;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.AdministratorPanel);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.DeactivateAccess();

        // Keycloak-first: disable the account at the identity provider before committing
        // IsActive=false. If the sync throws, UpdateAsync never runs, so both stores stay active —
        // no half-closed gap where a deactivated app user still receives fresh JWTs. Mirrors
        // AssignUserRole. See ADR-0007 (frontend/docs/adr/0007-role-authority-app-database).
        await _identityProviderAdmin.SyncUserActiveStateAsync(user.ExternalIdentityId, isActive: false, cancellationToken);

        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}
