namespace umbral_backend.Domain.Exceptions;

public sealed class StageSnapshotMustContainSubstagesException : DomainException
{
    public StageSnapshotMustContainSubstagesException()
        : base("A stage snapshot must contain at least one substage.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
