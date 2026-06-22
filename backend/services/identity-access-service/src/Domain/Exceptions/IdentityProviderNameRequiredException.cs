namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderNameRequiredException : DomainException
{
    public IdentityProviderNameRequiredException()
        : base("Identity provider name is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
