using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Services;

/// <summary>
/// Decides whether a <see cref="Entities.LiveSession"/> may be created from a
/// mission. Canon (ddd_solution_model.md) requires every session to originate
/// from an <b>active, runtime-ready</b> mission, so a deactivated or not-yet-ready
/// mission is rejected before any session is built.
/// </summary>
public sealed class SessionCreationPolicy
{
    private const string InactiveReason = "the mission is inactive";
    private const string NotRuntimeReadyReason = "the mission is not runtime-ready";

    public void EnsureMissionEligible(int missionId, bool isActive, bool isReady)
    {
        if (!isActive)
        {
            throw new MissionNotEligibleForSessionCreationException(missionId, InactiveReason);
        }

        if (!isReady)
        {
            throw new MissionNotEligibleForSessionCreationException(missionId, NotRuntimeReadyReason);
        }
    }
}
