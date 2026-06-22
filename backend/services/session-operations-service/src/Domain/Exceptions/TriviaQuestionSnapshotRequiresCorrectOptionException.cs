namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSnapshotRequiresCorrectOptionException : DomainException
{
    public TriviaQuestionSnapshotRequiresCorrectOptionException()
        : base("A trivia snapshot question must define at least one correct option.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
