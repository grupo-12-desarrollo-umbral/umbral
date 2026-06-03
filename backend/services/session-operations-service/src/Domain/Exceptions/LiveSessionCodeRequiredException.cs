namespace umbral_backend.Domain.Exceptions;

public sealed class LiveSessionCodeRequiredException : Exception
{
    public LiveSessionCodeRequiredException()
        : base("Session code is required.")
    {
    }
}
