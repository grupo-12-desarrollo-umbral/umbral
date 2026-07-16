namespace umbral_backend.Application.Common.Interfaces;

// The synchronous, fail-closed runtime block (#91, Tier 1): every participant-callable runtime path
// re-checks the identity-access eligibility fact and, on deny, applies a Participation Block before rejecting.
// Returns the access fact it validated so callers that also need the eligible-teams whitelist (reconnect's
// first-join branch) can reuse it instead of making a second identity-access round-trip.
public interface IRuntimeParticipationGuard
{
    Task<ParticipantEligibleTeamsDto> EnsureAllowedAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
