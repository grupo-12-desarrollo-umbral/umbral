namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionTimerExceedsMaximumException : DomainException
{
    public QuestionTimerExceedsMaximumException()
        : base("Trivia question timer cannot exceed 30 seconds.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
