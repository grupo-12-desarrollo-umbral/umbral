namespace umbral_backend.Domain.Exceptions;

public sealed class TeamIdentityRequiredException : Exception
{
    public TeamIdentityRequiredException()
        : base("Team identity is required.")
    {
    }
}
