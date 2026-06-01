namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaOptionTextRequiredException : Exception
{
    public TriviaOptionTextRequiredException()
        : base("Trivia option text is required.")
    {
    }
}
