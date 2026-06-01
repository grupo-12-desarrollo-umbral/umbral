namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizTitleRequiredException : Exception
{
    public TriviaQuizTitleRequiredException()
        : base("Trivia quiz title is required.")
    {
    }
}
