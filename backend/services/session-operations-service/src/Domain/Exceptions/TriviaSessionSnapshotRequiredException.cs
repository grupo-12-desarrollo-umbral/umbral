namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSessionSnapshotRequiredException : Exception
{
    public TriviaSessionSnapshotRequiredException()
        : base("A trivia session requires a fixed trivia snapshot at creation time.")
    {
    }
}
