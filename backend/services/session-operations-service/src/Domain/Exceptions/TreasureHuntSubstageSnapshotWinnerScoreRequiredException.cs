namespace umbral_backend.Domain.Exceptions;

public sealed class TreasureHuntSubstageSnapshotWinnerScoreRequiredException : Exception
{
    public TreasureHuntSubstageSnapshotWinnerScoreRequiredException()
        : base("A treasure-hunt substage snapshot must define a winner score.")
    {
    }
}
