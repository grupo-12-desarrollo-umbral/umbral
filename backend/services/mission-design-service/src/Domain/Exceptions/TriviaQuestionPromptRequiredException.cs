namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionPromptRequiredException : DomainException
{
    public TriviaQuestionPromptRequiredException()
        : base("Trivia question prompt is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
