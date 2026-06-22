namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaOptionSequenceOrderMustBePositiveException : DomainException
{
    public TriviaOptionSequenceOrderMustBePositiveException()
        : base("Trivia option sequence order must be a positive integer.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
