namespace umbral_backend.Domain.Exceptions;

// A ranking write lost a race: another recalculation committed between this one's read and its save.
// Thrown by RankingRepository so the Application layer can recognise a projection-concurrency loss
// without referencing EF or Npgsql (Application has no persistence dependency by design).
//
// This is Scoring Monitoring's own token — deliberately not the Session Operations
// ConcurrentModificationException. The two bounded contexts must not share implementation types, so
// each owns its concurrency exception, translation, tracking reset, and retry behaviour.
//
// The scoring ConcurrencyRetryBehaviour catches this and re-runs RecalculateRankingCommandHandler
// against fresh state: the retry re-reads every committed score entry and the current ranking, then
// derives the next CalculationVersion from the winner's row. This exception only reaches the client
// when the retry budget is exhausted — Conflict rather than 500, since the request was well-formed.
public sealed class ConcurrentRankingModificationException : DomainException
{
    public ConcurrentRankingModificationException(Exception innerException)
        : base("The ranking was modified concurrently by another recalculation.", innerException)
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string? PublicDetail =>
        "The ranking changed while the request was in flight. Try again.";
}
