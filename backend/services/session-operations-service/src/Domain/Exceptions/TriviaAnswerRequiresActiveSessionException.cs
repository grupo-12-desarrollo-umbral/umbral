using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

// Session-state rejection: a team answer is accepted only while the session is Active and admitting
// the synchronized trivia question. Paused/Finished/Cancelled (and pre-start states) reject.
public sealed class TriviaAnswerRequiresActiveSessionException : DomainException
{
    public TriviaAnswerRequiresActiveSessionException(SessionState currentState)
        : base($"Registering a trivia answer requires an Active live session. Current state is '{currentState}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
