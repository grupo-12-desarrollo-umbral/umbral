namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionTitleRequiredException : DomainException
{
    public LiveSessionTitleRequiredException()
        : base("Session title is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
