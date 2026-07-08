using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class OpenTeamSelectionClosedException : DomainException
{
    public OpenTeamSelectionClosedException(SessionState state)
        : base($"Open Team Selection is only available before the session starts; the session is '{state}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;
}
