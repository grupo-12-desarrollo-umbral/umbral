namespace umbral_backend.Domain.Exceptions;

public sealed class StageSnapshotIdRequiredException : Exception
{
    public StageSnapshotIdRequiredException()
        : base("Stage snapshot id is required.")
    {
    }
}
