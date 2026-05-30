namespace umbral_backend.Domain.Exceptions;

public sealed class IdentityProviderSessionExpirationInvalidException : Exception
{
    public IdentityProviderSessionExpirationInvalidException()
        : base("Identity provider session expiration must be after its start time.")
    {
    }
}
