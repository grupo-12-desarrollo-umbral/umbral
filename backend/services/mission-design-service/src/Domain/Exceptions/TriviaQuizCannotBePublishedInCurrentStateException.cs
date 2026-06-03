using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizCannotBePublishedInCurrentStateException : Exception
{
    public TriviaQuizCannotBePublishedInCurrentStateException(TriviaQuizStatus status)
        : base($"Trivia quiz in status '{status}' cannot be published.")
    {
    }
}
