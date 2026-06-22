namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuestionMustHaveExactlyOneCorrectOptionException : DomainException
{
    public TriviaQuestionMustHaveExactlyOneCorrectOptionException()
        : base("Trivia question must define exactly one correct option.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
