using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizNotEditableException : DomainException
{
    public TriviaQuizNotEditableException(TriviaQuizStatus status)
        : base($"Trivia quiz in status '{status}' cannot be edited.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
