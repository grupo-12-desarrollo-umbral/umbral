namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSessionSnapshotMustContainQuestionsException : DomainException
{
    public TriviaSessionSnapshotMustContainQuestionsException()
        : base("A trivia session snapshot must contain at least one question.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
