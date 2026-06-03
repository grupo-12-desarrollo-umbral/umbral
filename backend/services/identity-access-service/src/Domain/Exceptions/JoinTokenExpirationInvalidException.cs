namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenExpirationInvalidException : Exception
{
    public JoinTokenExpirationInvalidException()
        : base("A join token expiration must be later than its issuance time.")
    {
    }
}
