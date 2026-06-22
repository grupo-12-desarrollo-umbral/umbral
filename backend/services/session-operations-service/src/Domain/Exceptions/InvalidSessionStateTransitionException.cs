using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class InvalidSessionStateTransitionException : DomainException
{
    public InvalidSessionStateTransitionException(SessionState currentState, SessionState nextState)
        : base($"Session cannot transition from '{currentState}' to '{nextState}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;

    public override string ErrorCode => "invalid-state-transition";
}
