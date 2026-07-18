using FluentValidation.Results;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<InviteUserCommandHandler> _logger;

    public InviteUserCommandHandler(
        IUserRepository userRepository,
        IIdentityProviderAdminService identityProviderAdmin,
        ILogger<InviteUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _identityProviderAdmin = identityProviderAdmin;
        _logger = logger;
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
        // to email a disabled user), then gets its role, the invitation email, and finally the local
        // record. EVERY fallible step — including the local DB write — runs inside the try, so any
        // failure deletes the Keycloak account and no orphan is left behind. The DB commit (AddAsync,
        // which SaveChanges synchronously) is deliberately LAST: once it succeeds there is nothing left
        // that can fail and trigger a compensating delete of a now-persisted user, so the two stores can
        // never diverge. Sending the email before the commit only risks a dead invitation link on the
        // rare DB failure (the admin simply re-invites) — far cheaper than a silent desync. (#2 invite-compensation)
        var externalIdentityId = await _identityProviderAdmin.CreateUserAsync(command.Email, cancellationToken);

        User user;
        try
        {
            await _identityProviderAdmin.SyncUserRoleAsync(externalIdentityId, role, cancellationToken);
            await _identityProviderAdmin.SendExecuteActionsEmailAsync(externalIdentityId, cancellationToken);

            user = User.Invite(externalIdentityId, command.Email, role);
            await _userRepository.AddAsync(user, cancellationToken);
        }
        catch
        {
            // Compensate the orphaned Keycloak account, but never let the cleanup mask the original
            // failure: if the delete itself throws, log it and rethrow the original cause so the caller
            // sees why the invitation actually failed. (#2 invite-compensation)
            try
            {
                await _identityProviderAdmin.DeleteUserAsync(externalIdentityId, CancellationToken.None);
            }
            catch (Exception compensationException)
            {
                _logger.LogError(
                    compensationException,
                    "Failed to delete orphaned identity-provider account {ExternalIdentityId} while compensating a failed invitation.",
                    externalIdentityId);
            }

            throw;
        }

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
