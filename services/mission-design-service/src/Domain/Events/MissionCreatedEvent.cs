using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionCreatedEvent : BaseEvent
{
    public MissionCreatedEvent(Mission mission)
    {
        Mission = mission;
    }

    public Mission Mission { get; }
}
