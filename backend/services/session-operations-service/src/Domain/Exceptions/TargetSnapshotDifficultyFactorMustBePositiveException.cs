namespace umbral_backend.Domain.Exceptions;

public sealed class TargetSnapshotDifficultyFactorMustBePositiveException : DomainException
{
    public TargetSnapshotDifficultyFactorMustBePositiveException()
        : base("TargetSnapshot difficulty factor must be a positive integer.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
