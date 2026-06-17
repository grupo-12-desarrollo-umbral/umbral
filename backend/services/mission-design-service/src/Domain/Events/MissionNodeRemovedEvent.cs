using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class MissionNodeRemovedEvent : BaseEvent
{
    public MissionNodeRemovedEvent(Mission mission, MissionNode node)
    {
        Mission = mission;
        Node = node;
    }

    public Mission Mission { get; }

    public MissionNode Node { get; }
}
