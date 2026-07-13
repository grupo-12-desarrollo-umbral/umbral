using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SessionNotActiveForClueReleaseException : DomainException
{
    public SessionNotActiveForClueReleaseException(SessionState currentState)
        : base($"Clue release requires an Active live session. Current state is '{currentState}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
