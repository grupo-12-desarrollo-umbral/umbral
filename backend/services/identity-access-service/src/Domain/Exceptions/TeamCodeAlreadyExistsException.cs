namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCodeAlreadyExistsException : DomainException
{
    public TeamCodeAlreadyExistsException(string teamCode)
        : base($"Team code '{teamCode}' already exists.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
