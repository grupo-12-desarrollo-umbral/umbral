namespace umbral_backend.Domain.Exceptions;

public sealed class ExternalIdentityIdRequiredException : Exception
{
    public ExternalIdentityIdRequiredException()
        : base("External identity id is required.")
    {
    }
}
