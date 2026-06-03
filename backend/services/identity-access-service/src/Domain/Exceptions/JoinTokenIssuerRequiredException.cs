namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenIssuerRequiredException : Exception
{
    public JoinTokenIssuerRequiredException()
        : base("A join token issuer is required.")
    {
    }
}
