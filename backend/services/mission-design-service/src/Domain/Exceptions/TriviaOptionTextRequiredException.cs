namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaOptionTextRequiredException : DomainException
{
    public TriviaOptionTextRequiredException()
        : base("Trivia option text is required.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
