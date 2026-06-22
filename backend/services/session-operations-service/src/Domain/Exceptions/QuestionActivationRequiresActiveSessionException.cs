using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class QuestionActivationRequiresActiveSessionException : DomainException
{
    public QuestionActivationRequiresActiveSessionException(SessionState currentState)
        : base($"Question activation requires an Active live session. Current state is '{currentState}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
