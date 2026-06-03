using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Exceptions;

public sealed class JoinTokenReplayRejectedException : Exception
{
    public JoinTokenReplayRejectedException(Guid joinTokenId, JoinTokenStatus status)
        : base($"Join token '{joinTokenId}' cannot be used because its status is '{status}'.")
    {
    }
}
