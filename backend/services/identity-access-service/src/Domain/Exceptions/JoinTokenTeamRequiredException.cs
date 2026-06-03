namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenTeamRequiredException : Exception
{
    public JoinTokenTeamRequiredException()
        : base("A join token must reference a team.")
    {
    }
}
