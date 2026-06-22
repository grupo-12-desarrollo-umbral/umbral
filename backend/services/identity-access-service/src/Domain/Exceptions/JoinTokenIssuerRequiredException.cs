namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenIssuerRequiredException : DomainException
{
    public JoinTokenIssuerRequiredException()
        : base("A join token issuer is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
