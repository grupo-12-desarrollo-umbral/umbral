namespace umbral_backend.Domain.Exceptions;

public sealed class TreasureHuntTargetSnapshotScoreRequiredException : DomainException
{
    public TreasureHuntTargetSnapshotScoreRequiredException()
        : base("Every target in a treasure-hunt substage snapshot must define a positive score.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
