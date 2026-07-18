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

    // Create an identity-provider account for a self-registering participant (ADR-0016 §1): enabled,
    // email unverified, and carrying the password the person chose on the custom mobile form. Unlike
    // the invitation create above, a credential is set up front — the participant supplies it, rather
    // than setting it later from an action email. Returns the provider subject id. Throws
    // EmailAlreadyRegisteredException if the address is already registered at the provider. Password
    // policy stays the provider's: the credential is forwarded on this call, never validated here. The
    // account is created enabled because the provider refuses to send the verification email to a
    // disabled user; the caller compensates via DeleteUserAsync if a later step fails.
    Task<string> CreateParticipantAsync(
        string displayName, string email, string password, CancellationToken cancellationToken);

    // Ask the identity provider to email the invitee a required-actions link for UPDATE_PASSWORD and
    // VERIFY_EMAIL. Sending relies on the provider's configured SMTP server.
    Task SendExecuteActionsEmailAsync(string externalIdentityId, CancellationToken cancellationToken);

    // Ask the identity provider to email a self-registered participant a VERIFY_EMAIL-only action link
    // (they already set their password on the form, so no UPDATE_PASSWORD). Sending relies on the
    // provider's configured SMTP server.
    Task SendVerifyEmailAsync(string externalIdentityId, CancellationToken cancellationToken);

    // Look up the provider subject id for an exact email match, or null if no account exists. Backs the
    // anonymous forgot-password flow (ADR-0016 §1): the handler stays silent when this returns null, so
    // the endpoint never reveals whether an address is registered.
    Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken);

    // Ask the identity provider to email an UPDATE_PASSWORD-only action link so the user can reset their
    // credential. Used by the anonymous forgot-password flow. Sending relies on the provider's
    // configured SMTP server.
    Task SendResetPasswordEmailAsync(string externalIdentityId, CancellationToken cancellationToken);

    // Compensating delete: remove a provider account created during a failed invitation so no orphan
    // is left behind. Idempotent — a missing account is treated as already removed.
    Task DeleteUserAsync(string externalIdentityId, CancellationToken cancellationToken);

    Task SyncUserRoleAsync(string externalIdentityId, Role newRole, CancellationToken cancellationToken);

    // Toggle the identity provider account's enabled state. isActive=false disables it so a
    // deactivated user stops receiving fresh JWTs; isActive=true re-enables (reactivation).
    Task SyncUserActiveStateAsync(string externalIdentityId, bool isActive, CancellationToken cancellationToken);
}
