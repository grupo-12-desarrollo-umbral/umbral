namespace umbral_backend.Domain.Exceptions;

public sealed class SubstageSnapshotIdRequiredException : Exception
{
    public SubstageSnapshotIdRequiredException()
        : base("Substage snapshot id is required.")
    {
    }
}
