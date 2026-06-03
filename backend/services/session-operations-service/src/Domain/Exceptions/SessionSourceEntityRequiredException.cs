namespace umbral_backend.Domain.Exceptions;

public sealed class SessionSourceEntityRequiredException : Exception
{
    public SessionSourceEntityRequiredException()
        : base("Session source entity id is required.")
    {
    }
}
