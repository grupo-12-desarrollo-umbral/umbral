namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionTitleRequiredException : Exception
{
    public LiveSessionTitleRequiredException()
        : base("Session title is required.")
    {
    }
}
