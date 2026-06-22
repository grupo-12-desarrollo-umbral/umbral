namespace umbral_backend.Domain.Exceptions;

public sealed class TreasureHuntSubstageSnapshotWinnerScoreRequiredException : DomainException
{
    public TreasureHuntSubstageSnapshotWinnerScoreRequiredException()
        : base("A treasure-hunt substage snapshot must define a winner score.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
