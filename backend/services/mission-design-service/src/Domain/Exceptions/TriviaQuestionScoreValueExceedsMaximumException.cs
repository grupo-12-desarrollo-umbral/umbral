namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionScoreValueExceedsMaximumException : DomainException
{
    public TriviaQuestionScoreValueExceedsMaximumException()
        : base("Trivia question score value cannot exceed 100.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
