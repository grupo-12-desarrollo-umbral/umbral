using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

public sealed class DefaultPenaltyPolicy : IPenaltyPolicy
{
    public void ValidateEligibility(Guid liveSessionId, Guid teamId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (liveSessionId == Guid.Empty)
        {
            throw new PenaltyNotEligibleException(liveSessionId, teamId, reason);
        }

        if (teamId == Guid.Empty)
        {
            throw new PenaltyNotEligibleException(liveSessionId, teamId, reason);
        }
    }
}
