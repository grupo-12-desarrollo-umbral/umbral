namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderSessionExpirationInvalidException : DomainException
{
    public IdentityProviderSessionExpirationInvalidException()
        : base("Identity provider session expiration must be after its start time.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
