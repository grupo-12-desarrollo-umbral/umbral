namespace umbral_backend.Domain.Exceptions;

public sealed class SessionCodeRequiredException : Exception
{
    public SessionCodeRequiredException()
        : base("A session code is required.")
    {
    }
}
