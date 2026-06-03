using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class SessionSourceDoesNotMatchModeException : Exception
{
    public SessionSourceDoesNotMatchModeException(SessionMode mode, SessionSourceType sourceType)
        : base($"Session mode '{mode}' cannot use source type '{sourceType}'.")
    {
    }
}
