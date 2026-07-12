namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSnapshotScoreMustBePositiveException : DomainException
{
    public TargetSnapshotScoreMustBePositiveException()
        : base("TargetSnapshot score must be a positive integer.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
