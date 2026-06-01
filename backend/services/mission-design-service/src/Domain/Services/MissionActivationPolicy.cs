using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Services;

public static class MissionActivationPolicy
{
    public static MissionActivation DetermineActivationState(bool isActive, bool satisfiesStructureRequirements)
    {
        if (!isActive)
        {
            return MissionActivation.Inactive;
        }

        return satisfiesStructureRequirements
            ? MissionActivation.Ready
            : MissionActivation.Draft;
    }
}
