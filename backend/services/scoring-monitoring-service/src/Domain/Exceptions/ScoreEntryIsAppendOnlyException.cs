namespace umbral_backend.Domain.Exceptions;

public sealed class ScoreEntryIsAppendOnlyException : DomainException
{
    public ScoreEntryIsAppendOnlyException()
        : base("Score entries are append-only and cannot be changed after registration.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
