namespace umbral_backend.Domain.Exceptions;

public sealed class SessionCodeRequiredException : DomainException
{
    public SessionCodeRequiredException()
        : base("A session code is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
