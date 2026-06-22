namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException : DomainException
{
    public TriviaQuestionSnapshotRequiresAtLeastTwoOptionsException()
        : base("A trivia snapshot question must define at least two options.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
