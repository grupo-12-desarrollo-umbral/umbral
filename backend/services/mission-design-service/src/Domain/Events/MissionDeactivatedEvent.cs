using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionDeactivatedEvent : BaseEvent
{
    public MissionDeactivatedEvent(Mission mission)
    {
        Mission = mission;
    }

    public Mission Mission { get; }
}
