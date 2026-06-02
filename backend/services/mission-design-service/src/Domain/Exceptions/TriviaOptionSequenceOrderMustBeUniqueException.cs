namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaOptionSequenceOrderMustBeUniqueException : Exception
{
    public TriviaOptionSequenceOrderMustBeUniqueException()
        : base("Trivia option sequence orders must be unique within a question.")
    {
    }
}
