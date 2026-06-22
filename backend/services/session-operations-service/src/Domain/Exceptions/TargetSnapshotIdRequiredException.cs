namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSnapshotIdRequiredException : DomainException
{
    public TargetSnapshotIdRequiredException()
        : base("Target snapshot id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
