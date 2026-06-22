namespace umbral_backend.Domain.Exceptions;

public sealed class StageSnapshotIdRequiredException : DomainException
{
    public StageSnapshotIdRequiredException()
        : base("Stage snapshot id is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
