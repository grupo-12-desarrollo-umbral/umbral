using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionActivatedEvent : BaseEvent
{
    public MissionActivatedEvent(Mission mission)
    {
        Mission = mission;
    }

    public Mission Mission { get; }
}
