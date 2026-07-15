namespace umbral_backend.Domain.Services;

public interface IPenaltyPolicy
{
    void ValidateEligibility(Guid liveSessionId, Guid teamId, string reason);
}
