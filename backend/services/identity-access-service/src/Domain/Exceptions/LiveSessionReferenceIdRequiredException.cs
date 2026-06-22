namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionReferenceIdRequiredException : DomainException
{
    public LiveSessionReferenceIdRequiredException()
        : base("A live session reference id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
