namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderSessionUserRequiredException : Exception
{
    public IdentityProviderSessionUserRequiredException()
        : base("An identity-provider session must belong to a user.")
    {
    }
}
