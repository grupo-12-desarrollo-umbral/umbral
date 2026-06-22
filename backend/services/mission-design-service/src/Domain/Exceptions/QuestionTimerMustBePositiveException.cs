namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionTimerMustBePositiveException : DomainException
{
    public QuestionTimerMustBePositiveException()
        : base("Trivia question timer must be positive.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
