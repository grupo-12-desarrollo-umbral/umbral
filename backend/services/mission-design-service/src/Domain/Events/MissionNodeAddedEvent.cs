using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionNodeAddedEvent : BaseEvent
{
    public MissionNodeAddedEvent(Mission mission, MissionNode node)
    {
        Mission = mission;
        Node = node;
    }

    public Mission Mission { get; }

    public MissionNode Node { get; }
}
