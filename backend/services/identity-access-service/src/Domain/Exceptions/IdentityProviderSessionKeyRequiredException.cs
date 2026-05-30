namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderSessionKeyRequiredException : Exception
{
    public IdentityProviderSessionKeyRequiredException()
        : base("Identity provider session key is required.")
    {
    }
}
