namespace umbral_backend.Domain.Exceptions;

public sealed class SessionSourceTriviaQuizIdRequiredException : Exception
{
    public SessionSourceTriviaQuizIdRequiredException()
        : base("Trivia quiz source id is required.")
    {
    }
}
