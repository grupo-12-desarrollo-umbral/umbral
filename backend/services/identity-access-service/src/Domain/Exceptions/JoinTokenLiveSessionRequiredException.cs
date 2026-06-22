namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenLiveSessionRequiredException : DomainException
{
    public JoinTokenLiveSessionRequiredException()
        : base("A join token must reference a live session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
