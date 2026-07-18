namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionTimerMustBePositiveException : DomainException
{
    public QuestionTimerMustBePositiveException()
        : base("Trivia question timer must be at least 15 seconds.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
