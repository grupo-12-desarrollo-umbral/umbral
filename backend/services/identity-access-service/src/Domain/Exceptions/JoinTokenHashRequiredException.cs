namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenHashRequiredException : DomainException
{
    public JoinTokenHashRequiredException()
        : base("A join token hash is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
