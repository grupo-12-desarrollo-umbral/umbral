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

    // Safe to expose: the message interpolates only session states (enums), never an identifier.
    // Surfacing the rejected from/to edge lets the API/UI show the specific transition that was
    // refused instead of a generic "not allowed from the current state" message.
    public override string? PublicDetail => Message;
}
