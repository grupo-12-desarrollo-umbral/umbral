namespace umbral_backend.Domain.Exceptions;

public sealed class TreasureHuntSubstageSnapshotMustContainTargetsException : Exception
{
    public TreasureHuntSubstageSnapshotMustContainTargetsException()
        : base("A treasure-hunt substage snapshot must contain at least one target.")
    {
    }
}
