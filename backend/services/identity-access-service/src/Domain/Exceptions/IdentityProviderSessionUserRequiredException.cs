namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderSessionUserRequiredException : DomainException
{
    public IdentityProviderSessionUserRequiredException()
        : base("An identity-provider session must belong to a user.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
