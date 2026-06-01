namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaOptionSequenceOrderMustBePositiveException : Exception
{
    public TriviaOptionSequenceOrderMustBePositiveException()
        : base("Trivia option sequence order must be a positive integer.")
    {
    }
}
