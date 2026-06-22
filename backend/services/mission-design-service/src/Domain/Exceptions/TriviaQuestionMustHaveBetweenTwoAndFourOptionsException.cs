namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionMustHaveBetweenTwoAndFourOptionsException : DomainException
{
    public TriviaQuestionMustHaveBetweenTwoAndFourOptionsException()
        : base("Trivia question must define between two and four options.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
