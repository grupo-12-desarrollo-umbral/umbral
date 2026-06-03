namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizCannotBeDestructivelyRemovedAfterUsageException : Exception
{
    public TriviaQuizCannotBeDestructivelyRemovedAfterUsageException()
        : base("A trivia quiz that has already been used in a session cannot be removed destructively.")
    {
    }
}
