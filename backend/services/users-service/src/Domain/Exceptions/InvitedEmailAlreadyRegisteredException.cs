namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Raised when an invitation targets an email address that is already registered — either as a
/// local user record or as an existing Keycloak account. Prevents a duplicate identity being
/// provisioned behind the same address.
/// </summary>
public sealed class InvitedEmailAlreadyRegisteredException : DomainException
{
    public InvitedEmailAlreadyRegisteredException(string email)
        : base($"A user with email '{email}' already exists.")
    {
    }

    public InvitedEmailAlreadyRegisteredException(string email, Exception innerException)
        : base($"A user with email '{email}' already exists.", innerException)
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: the caller supplied the address and the uniqueness rule is client-actionable;
    // the interpolated value stays in the diagnostic Message only.
    public override string? PublicDetail => "A user with this email address already exists.";
}
