namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenLiveSessionRequiredException : Exception
{
    public JoinTokenLiveSessionRequiredException()
        : base("A join token must reference a live session.")
    {
    }
}
