namespace umbral_backend.Domain.Exceptions;

public sealed class TeamCodeAlreadyExistsException : DomainException
{
    public TeamCodeAlreadyExistsException(string teamCode)
        : base($"Team code '{teamCode}' already exists.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    // Safe to expose: the caller supplied the code and the uniqueness rule is client-actionable;
    // the interpolated value stays in the diagnostic Message only.
    public override string? PublicDetail => "The team code already exists.";
}
