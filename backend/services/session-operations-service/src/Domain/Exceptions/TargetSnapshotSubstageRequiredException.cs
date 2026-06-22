namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSnapshotSubstageRequiredException : DomainException
{
    public TargetSnapshotSubstageRequiredException()
        : base("Target snapshots must belong to a substage snapshot.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
