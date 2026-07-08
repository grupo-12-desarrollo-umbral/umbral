namespace umbral_backend.Application.Common.Interfaces;

// The synchronous, fail-closed runtime block (#91, Tier 1): every participant-callable runtime path
// re-checks Users membership access and, on deny, applies a Participation Block before rejecting.
public interface IRuntimeParticipationGuard
{
    Task EnsureAllowedAsync(Guid liveSessionId, Guid teamId, string? token, CancellationToken cancellationToken);
}
