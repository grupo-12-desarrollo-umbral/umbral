namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenExpirationInvalidException : DomainException
{
    public JoinTokenExpirationInvalidException()
        : base("A join token expiration must be later than its issuance time.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
