using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TargetRemovedFromSubstageEvent : BaseEvent
{
    public TargetRemovedFromSubstageEvent(Mission mission, Substage substage, int targetId)
    {
        Mission = mission;
        Substage = substage;
        TargetId = targetId;
    }

    public Mission Mission { get; }

    public Substage Substage { get; }

    public int TargetId { get; }
}
