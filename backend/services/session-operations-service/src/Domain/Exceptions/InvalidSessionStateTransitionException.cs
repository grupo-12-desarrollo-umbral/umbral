using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class InvalidSessionStateTransitionException : Exception
{
    public InvalidSessionStateTransitionException(SessionState currentState, SessionState nextState)
        : base($"Session cannot transition from '{currentState}' to '{nextState}'.")
    {
    }
}
