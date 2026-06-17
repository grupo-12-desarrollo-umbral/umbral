using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class TargetUpdatedEvent : BaseEvent
{
    public TargetUpdatedEvent(Mission mission, Substage substage, Target target)
    {
        Mission = mission;
        Substage = substage;
        Target = target;
    }

    public Mission Mission { get; }

    public Substage Substage { get; }

    public Target Target { get; }
}
