namespace umbral_backend.Domain.Exceptions;

public sealed class ClueSnapshotIdRequiredException : DomainException
{
    public ClueSnapshotIdRequiredException()
        : base("Clue snapshot id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
