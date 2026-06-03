namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSnapshotRequiresCorrectOptionException : Exception
{
    public TriviaQuestionSnapshotRequiresCorrectOptionException()
        : base("A trivia snapshot question must define at least one correct option.")
    {
    }
}
