using umbral_backend.Domain.Entities;

namespace umbral_backend.Domain.Events;

public sealed class ClueAssociatedWithTargetEvent : BaseEvent
{
    public ClueAssociatedWithTargetEvent(Mission mission, Substage substage, Target target, Clue clue)
    {
        Mission = mission;
        Substage = substage;
        Target = target;
        Clue = clue;
    }

    public Mission Mission { get; }

    public Substage Substage { get; }

    public Target Target { get; }

    public Clue Clue { get; }
}
