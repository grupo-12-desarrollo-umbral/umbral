namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSubstageSnapshotCannotDeclareWinnerScoreException : DomainException
{
    public TriviaSubstageSnapshotCannotDeclareWinnerScoreException()
        : base("A trivia substage snapshot cannot declare a winner score.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
