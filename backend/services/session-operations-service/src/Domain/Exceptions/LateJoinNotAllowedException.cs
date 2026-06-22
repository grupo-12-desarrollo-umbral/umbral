using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class LateJoinNotAllowedException : DomainException
{
    public LateJoinNotAllowedException(SessionState state)
        : base($"New participant joins are not allowed while the session is '{state}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Forbidden;
}
