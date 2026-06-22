namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSessionSnapshotRequiredException : DomainException
{
    public TriviaSessionSnapshotRequiredException()
        : base("A trivia session requires a fixed trivia snapshot at creation time.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
