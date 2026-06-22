namespace umbral_backend.Domain.Exceptions;

public sealed class StageSnapshotMustContainSubstagesException : Exception
{
    public StageSnapshotMustContainSubstagesException()
        : base("A stage snapshot must contain at least one substage.")
    {
    }
}
