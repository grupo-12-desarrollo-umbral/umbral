namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSessionSnapshotMustContainQuestionsException : Exception
{
    public TriviaSessionSnapshotMustContainQuestionsException()
        : base("A trivia session snapshot must contain at least one question.")
    {
    }
}
