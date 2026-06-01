namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizDescriptionRequiredException : Exception
{
    public TriviaQuizDescriptionRequiredException()
        : base("Trivia quiz description is required.")
    {
    }
}
