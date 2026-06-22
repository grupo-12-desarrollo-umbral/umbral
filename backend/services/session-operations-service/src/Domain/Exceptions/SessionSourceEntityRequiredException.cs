namespace umbral_backend.Domain.Exceptions;

public sealed class SessionSourceEntityRequiredException : DomainException
{
    public SessionSourceEntityRequiredException()
        : base("Session source entity id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
