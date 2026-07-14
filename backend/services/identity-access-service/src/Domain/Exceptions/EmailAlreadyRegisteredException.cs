namespace umbral_backend.Domain.Exceptions;

/// <summary>
/// Raised when a self-registration targets an email address Keycloak already holds an account for.
/// The register path does not track uniqueness itself (no local record exists until first sign-in);
/// it surfaces Keycloak's conflict as a clean 409 rather than re-implementing the check.
/// </summary>
public sealed class EmailAlreadyRegisteredException : DomainException
{
    public EmailAlreadyRegisteredException(string email)
        : base($"A user with email '{email}' already exists.")
    {
    }

    public EmailAlreadyRegisteredException(string email, Exception innerException)
        : base($"A user with email '{email}' already exists.", innerException)
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: the caller supplied the address and the uniqueness rule is client-actionable;
    // the interpolated value stays in the diagnostic Message only.
    public override string? PublicDetail => "A user with this email address already exists.";
}
