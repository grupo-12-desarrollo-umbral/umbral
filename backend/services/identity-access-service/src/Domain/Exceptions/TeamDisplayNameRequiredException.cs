namespace umbral_backend.Domain.Exceptions;

public sealed class TeamDisplayNameRequiredException : Exception
{
    public TeamDisplayNameRequiredException()
        : base("Team display name is required.")
    {
    }
}
