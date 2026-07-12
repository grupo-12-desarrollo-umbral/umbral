using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Common.Interfaces;

public interface IIdentityProviderAdminService
{
    // Create an identity-provider account for an invited user: enabled, email unverified, and carrying
    // no credentials (the invitee sets their own password via the emailed action link). Returns the
    // provider subject id. Throws InvitedEmailAlreadyRegisteredException if the address is already
    // registered at the provider. The account is created enabled because the provider refuses to send
    // a required-actions email to a disabled user; the caller compensates via DeleteUserAsync if a
    // later step fails, so a failed invitation never leaves an orphaned account.
    Task<string> CreateUserAsync(string email, CancellationToken cancellationToken);

    // Ask the identity provider to email the invitee a required-actions link for UPDATE_PASSWORD and
    // VERIFY_EMAIL. Sending relies on the provider's configured SMTP server.
    Task SendExecuteActionsEmailAsync(string externalIdentityId, CancellationToken cancellationToken);

    // Compensating delete: remove a provider account created during a failed invitation so no orphan
    // is left behind. Idempotent — a missing account is treated as already removed.
    Task DeleteUserAsync(string externalIdentityId, CancellationToken cancellationToken);

    Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken);

    // Toggle the identity provider account's enabled state. isActive=false disables it so a
    // deactivated user stops receiving fresh JWTs; isActive=true re-enables (reactivation).
    Task SyncUserActiveStateAsync(string externalIdentityId, bool isActive, CancellationToken cancellationToken);
}
