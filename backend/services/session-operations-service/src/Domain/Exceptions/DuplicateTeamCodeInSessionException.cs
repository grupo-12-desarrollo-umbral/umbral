namespace umbral_backend.Domain.Exceptions;

public sealed class DuplicateTeamCodeInSessionException : DomainException
{
    public DuplicateTeamCodeInSessionException(string teamCode)
        : base($"Team code '{teamCode}' is already registered in the session.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
