namespace umbral_backend.Domain.Exceptions;

// A write lost a race: another writer committed between this request's read and its save. Thrown by
// the repository so the Application layer can recognise a concurrency loss without referencing EF or
// Npgsql (Application has no persistence dependency by design).
//
// ConcurrencyRetryBehaviour catches this and re-runs the handler against fresh state, which is what
// resolves it in practice: the retried attempt re-reads the winner's row and reaches the same
// outcome the sequential path would have. This exception only reaches the client when the retry
// budget is exhausted — Conflict rather than 500, since the request was well-formed.
public sealed class ConcurrentModificationException : DomainException
{
    public ConcurrentModificationException(Exception innerException)
        : base("The aggregate was modified concurrently by another writer.", innerException)
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string? PublicDetail =>
        "The session changed while the request was in flight. Try again.";
}
