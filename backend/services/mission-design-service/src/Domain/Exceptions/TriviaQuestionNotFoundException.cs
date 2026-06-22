namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionNotFoundException : DomainException
{
    public TriviaQuestionNotFoundException(int triviaQuestionId)
        : base($"Trivia question with id '{triviaQuestionId}' was not found in the quiz.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.NotFound;
}
