namespace umbral_backend.Domain.Exceptions;

public sealed class ClueSnapshotSubstageRequiredException : DomainException
{
    public ClueSnapshotSubstageRequiredException()
        : base("Clue snapshot must reference a substage snapshot.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
