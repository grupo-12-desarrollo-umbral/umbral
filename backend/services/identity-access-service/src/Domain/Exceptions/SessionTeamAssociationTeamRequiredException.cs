namespace umbral_backend.Domain.Exceptions;

public sealed class SessionTeamAssociationTeamRequiredException : DomainException
{
    public SessionTeamAssociationTeamRequiredException()
        : base("A team id is required to associate a team with a session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
