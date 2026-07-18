using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.Users.Commands.ReactivateUser;

public sealed class ReactivateUserCommandHandler : IRequestHandler<ReactivateUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentActor _currentActor;
    private readonly AccessPolicy _accessPolicy;
    private readonly IIdentityProviderAdminService _identityProviderAdmin;

    public ReactivateUserCommandHandler(
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

    public async Task Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        var actor = await _currentActor.GetActorAsync(cancellationToken);

        _accessPolicy.EnsureCanAccess(actor, ProtectedCapability.AdministratorPanel);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), request.UserId);

        user.ReactivateAccess();

        // Keycloak-first: re-enable the account at the identity provider before committing
        // IsActive=true. If the sync throws, UpdateAsync never runs, so both stores stay disabled —
        // no half-open gap where the app DB says active but the user still can't log in. Mirrors
        // DeactivateUser. See ADR-0007 (frontend/docs/adr/0007-role-authority-app-database).
        await _identityProviderAdmin.SyncUserActiveStateAsync(user.ExternalIdentityId, isActive: true, cancellationToken);

        await _userRepository.UpdateAsync(user, cancellationToken);
    }
}
