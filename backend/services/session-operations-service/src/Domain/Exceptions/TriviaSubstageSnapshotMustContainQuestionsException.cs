namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaSubstageSnapshotMustContainQuestionsException : DomainException
{
    public TriviaSubstageSnapshotMustContainQuestionsException()
        : base("A trivia substage snapshot must contain at least one trivia question.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string ErrorCode => "trivia-substage-empty";
}
