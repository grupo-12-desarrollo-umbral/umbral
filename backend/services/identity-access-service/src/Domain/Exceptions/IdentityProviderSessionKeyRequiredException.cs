namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderSessionKeyRequiredException : DomainException
{
    public IdentityProviderSessionKeyRequiredException()
        : base("Identity provider session key is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
