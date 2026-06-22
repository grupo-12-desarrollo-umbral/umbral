namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizDescriptionRequiredException : DomainException
{
    public TriviaQuizDescriptionRequiredException()
        : base("Trivia quiz description is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
