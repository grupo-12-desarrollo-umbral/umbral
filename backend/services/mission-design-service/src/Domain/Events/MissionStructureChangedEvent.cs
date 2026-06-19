using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionStructureChangedEvent : BaseEvent
{
    public MissionStructureChangedEvent(Mission mission)
    {
        Mission = mission;
    }

    public Mission Mission { get; }
}
