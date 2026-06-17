using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// Top-level mission node. A <see cref="Stage"/> groups progression and must
/// contain one or more <see cref="Substage"/> children to be runtime-ready.
/// </summary>
public sealed class Stage : MissionNode
{
    private Stage()
    {
    }

    private Stage(string title, int sequenceOrder)
        : base(title, sequenceOrder)
    {
    }

    public override MissionNodeType NodeType => MissionNodeType.Stage;

    public IEnumerable<Substage> Substages => Children.OfType<Substage>();

    public static Stage Create(string title, int sequenceOrder)
    {
        return new Stage(title, sequenceOrder);
    }

    public Substage AddSubstage(Substage substage)
    {
        AddChild(substage);
        return substage;
    }

    protected override bool CanContain(MissionNodeType childType)
    {
        return childType == MissionNodeType.Substage;
    }
}
