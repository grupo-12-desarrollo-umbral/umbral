using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenReplayRejectedException : DomainException
{
    public JoinTokenReplayRejectedException(Guid joinTokenId, JoinTokenStatus status)
        : base($"Join token '{joinTokenId}' cannot be used because its status is '{status}'.")
    {
    }

    public override ErrorCategory Category => ErrorCategory.Conflict;
}
