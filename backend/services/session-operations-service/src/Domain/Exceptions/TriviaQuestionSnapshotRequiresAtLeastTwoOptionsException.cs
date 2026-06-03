namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException : Exception
{
    public TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException()
        : base("A trivia snapshot question must define at least two options.")
    {
    }
}
