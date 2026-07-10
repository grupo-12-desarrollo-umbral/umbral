namespace umbral_backend.Domain.Exceptions;

// No synchronized trivia question is currently active in the session's active trivia substage,
// so there is nothing to answer.
public sealed class TriviaAnswerRequiresActiveQuestionException : DomainException
{
    public TriviaAnswerRequiresActiveQuestionException()
        : base("Registering a trivia answer requires an active trivia question.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
