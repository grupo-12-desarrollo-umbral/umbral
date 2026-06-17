using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionNodeUpdatedEvent : BaseEvent
{
    public MissionNodeUpdatedEvent(Mission mission, MissionNode node)
    {
        Mission = mission;
        Node = node;
    }

    public Mission Mission { get; }

    public MissionNode Node { get; }
}
