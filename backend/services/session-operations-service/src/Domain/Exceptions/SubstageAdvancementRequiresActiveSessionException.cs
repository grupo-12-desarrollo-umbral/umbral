using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SubstageAdvancementRequiresActiveSessionException : DomainException
{
    public SubstageAdvancementRequiresActiveSessionException(SessionState currentState)
        : base($"Substage advancement requires an Active live session. Current state is '{currentState}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
