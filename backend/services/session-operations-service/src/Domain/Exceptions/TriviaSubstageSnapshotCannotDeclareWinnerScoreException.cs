namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSubstageSnapshotCannotDeclareWinnerScoreException : Exception
{
    public TriviaSubstageSnapshotCannotDeclareWinnerScoreException()
        : base("A trivia substage snapshot cannot declare a winner score.")
    {
    }
}
