namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenTeamRequiredException : DomainException
{
    public JoinTokenTeamRequiredException()
        : base("A join token must reference a team.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
