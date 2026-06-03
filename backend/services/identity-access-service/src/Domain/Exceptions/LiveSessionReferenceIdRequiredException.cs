namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionReferenceIdRequiredException : Exception
{
    public LiveSessionReferenceIdRequiredException()
        : base("A live session reference id is required.")
    {
    }
}
