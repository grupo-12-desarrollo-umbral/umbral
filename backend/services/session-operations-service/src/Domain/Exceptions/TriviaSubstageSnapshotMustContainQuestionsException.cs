namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSubstageSnapshotMustContainQuestionsException : Exception
{
    public TriviaSubstageSnapshotMustContainQuestionsException()
        : base("A trivia substage snapshot must contain at least one trivia question.")
    {
    }
}
