namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueMustBePositiveException : DomainException
{
    public TriviaQuestionScoreValueMustBePositiveException()
        : base("Trivia question score value must be positive.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
