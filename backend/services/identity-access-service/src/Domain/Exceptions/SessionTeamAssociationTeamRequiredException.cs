namespace umbral_backend.Domain.Exceptions;

public sealed class SessionTeamAssociationTeamRequiredException : Exception
{
    public SessionTeamAssociationTeamRequiredException()
        : base("A team id is required to associate a team with a session.")
    {
    }
}
