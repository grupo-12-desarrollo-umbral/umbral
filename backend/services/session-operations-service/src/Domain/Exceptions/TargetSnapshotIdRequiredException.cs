namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSnapshotIdRequiredException : Exception
{
    public TargetSnapshotIdRequiredException()
        : base("Target snapshot id is required.")
    {
    }
}
