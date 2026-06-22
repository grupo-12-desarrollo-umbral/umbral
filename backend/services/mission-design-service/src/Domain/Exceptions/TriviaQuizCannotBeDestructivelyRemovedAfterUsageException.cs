namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizCannotBeDestructivelyRemovedAfterUsageException : DomainException
{
    public TriviaQuizCannotBeDestructivelyRemovedAfterUsageException()
        : base("A trivia quiz that has already been used in a session cannot be removed destructively.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
