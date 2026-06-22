namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionCodeRequiredException : DomainException
{
    public LiveSessionCodeRequiredException()
        : base("Session code is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
