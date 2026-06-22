namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizTitleRequiredException : DomainException
{
    public TriviaQuizTitleRequiredException()
        : base("Trivia quiz title is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
