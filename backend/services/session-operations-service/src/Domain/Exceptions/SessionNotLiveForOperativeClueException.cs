using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SessionNotLiveForOperativeClueException : DomainException
{
    public SessionNotLiveForOperativeClueException(SessionState currentState)
        : base($"Adding an operative clue requires an Active or Paused live session. Current state is '{currentState}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
