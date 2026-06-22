namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSnapshotSubstageRequiredException : Exception
{
    public TargetSnapshotSubstageRequiredException()
        : base("Target snapshots must belong to a substage snapshot.")
    {
    }
}
