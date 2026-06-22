using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class TriviaQuizCannotBeArchivedInCurrentStateException : DomainException
{
    public TriviaQuizCannotBeArchivedInCurrentStateException(TriviaQuizStatus status)
        : base($"Trivia quiz in status '{status}' cannot be archived.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
