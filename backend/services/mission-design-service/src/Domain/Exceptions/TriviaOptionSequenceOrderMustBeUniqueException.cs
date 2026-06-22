namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaOptionSequenceOrderMustBeUniqueException : DomainException
{
    public TriviaOptionSequenceOrderMustBeUniqueException()
        : base("Trivia option sequence orders must be unique within a question.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
