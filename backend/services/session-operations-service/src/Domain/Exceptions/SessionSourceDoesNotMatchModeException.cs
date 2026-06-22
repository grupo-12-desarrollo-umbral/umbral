using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SessionSourceDoesNotMatchModeException : DomainException
{
    public SessionSourceDoesNotMatchModeException(SessionMode mode, SessionSourceType sourceType)
        : base($"Session mode '{mode}' cannot use source type '{sourceType}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Validation;
}
