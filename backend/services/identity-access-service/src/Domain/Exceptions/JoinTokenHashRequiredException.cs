namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenHashRequiredException : Exception
{
    public JoinTokenHashRequiredException()
        : base("A join token hash is required.")
    {
    }
}
