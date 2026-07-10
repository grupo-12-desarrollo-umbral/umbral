using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class LateJoinNotAllowedException : DomainException
{
    public LateJoinNotAllowedException(SessionState state)
        : base($"New participant joins are not allowed while the session is '{state}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;

    // Safe to expose: the message interpolates only the session state (an enum), never an
    // identifier, and mobile surfaces this reason verbatim through the LATE_JOIN_NOT_ALLOWED code.
    public override string? PublicDetail => Message;
}
