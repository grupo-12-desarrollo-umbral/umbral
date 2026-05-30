namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderNameRequiredException : Exception
{
    public IdentityProviderNameRequiredException()
        : base("Identity provider name is required.")
    {
    }
}
