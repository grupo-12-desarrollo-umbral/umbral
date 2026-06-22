namespace umbral_backend.Domain.Exceptions;

public sealed class TreasureHuntSubstageSnapshotMustContainTargetsException : DomainException
{
    public TreasureHuntSubstageSnapshotMustContainTargetsException()
        : base("A treasure-hunt substage snapshot must contain at least one target.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
