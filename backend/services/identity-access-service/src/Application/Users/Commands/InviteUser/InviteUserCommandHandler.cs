using FluentValidation.Results;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Users;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.Users.Commands.InviteUser;

using ValidationException = umbral_backend.Application.Common.Exceptions.ValidationException;

// Real subject wrapped by InviteUserAuthorizationProxy (the registered IRequestHandler): the proxy
// enforces the Administrator guard, this handler runs the invitation.
public sealed class InviteUserCommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IIdentityProviderAdminService _identityProviderAdmin;

    public InviteUserCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderAdminService identityProviderAdmin)
    {
        _userRepository = userRepository;
        _identityProviderAdmin = identityProviderAdmin;
    }

    public async Task<InviteUserResultDto> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        if (!TryParseRole(command.Role, out var role))
        {
            throw CreateUnknownRoleValidationException();
        }

        // Domain invariant: participants self-register. Rejected before any identity-provider account
        // is created, so an ineligible role never provisions a Keycloak user.
        User.EnsureInvitableRole(role);

        var existing = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existing is not null)
        {
            throw new InvitedEmailAlreadyRegisteredException(command.Email);
        }

        // Keycloak-first with a compensating delete: the account is created enabled (Keycloak refuses
        // to email a disabled user), then gets its role and the invitation email. If role assignment or
        // the email fails, the account is deleted so no orphaned account is left behind and the local
        // record is never written.
        var externalIdentityId = await _identityProviderAdmin.CreateUserAsync(command.Email, cancellationToken);

        try
        {
            await _identityProviderAdmin.SyncUserRoleAsync(externalIdentityId, role, cancellationToken);
            await _identityProviderAdmin.SendExecuteActionsEmailAsync(externalIdentityId, cancellationToken);
        }
        catch
        {
            await _identityProviderAdmin.DeleteUserAsync(externalIdentityId, CancellationToken.None);
            throw;
        }

        var user = User.Invite(externalIdentityId, command.Email, role);
        await _userRepository.AddAsync(user, cancellationToken);

        return new InviteUserResultDto(user.Id, user.Email, user.Role.ToString());
    }

    private static bool TryParseRole(string role, out Role parsedRole)
    {
        return Enum.TryParse(role, ignoreCase: true, out parsedRole) && Enum.IsDefined(parsedRole);
    }

    private static ValidationException CreateUnknownRoleValidationException()
    {
        return new ValidationException(
            new[]
            {
                new ValidationFailure(nameof(InviteUserCommand.Role), "Role must be a known Role value.")
            });
    }
}
