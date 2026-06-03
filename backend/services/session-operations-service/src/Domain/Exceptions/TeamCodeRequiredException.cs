namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCodeRequiredException : Exception
{
    public TeamCodeRequiredException()
        : base("Team code is required.")
    {
    }
}
