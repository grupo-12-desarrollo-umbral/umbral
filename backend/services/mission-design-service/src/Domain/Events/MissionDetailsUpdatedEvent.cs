using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionDetailsUpdatedEvent : BaseEvent
{
    public MissionDetailsUpdatedEvent(Mission mission)
    {
        Mission = mission;
    }

    public Mission Mission { get; }
}
