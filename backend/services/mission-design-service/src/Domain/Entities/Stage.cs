using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// Top-level mission node. A <see cref="Stage"/> groups progression and must
/// contain one or more <see cref="Substage"/> children to be runtime-ready.
/// </summary>
public sealed class Stage : MissionNode
{
    private readonly List<Substage> _substages = [];

    private Stage()
    {
    }

    private Stage(string title, int sequenceOrder)
        : base(title, sequenceOrder)
    {
    }

    public override MissionNodeType NodeType => MissionNodeType.Stage;

    public IEnumerable<Substage> Substages =>
        _substages.OrderBy(substage => substage.SequenceOrder).ToList().AsReadOnly();

    public static Stage Create(string title, int sequenceOrder)
    {
        return new Stage(title, sequenceOrder);
    }

    public Substage AddSubstage(Substage substage)
    {
        AddChild(substage);
        return substage;
    }

    protected override IEnumerable<MissionNode> ChildNodes => _substages;

    protected override void AddChildNode(MissionNode child)
    {
        _substages.Add((Substage)child);
    }

    protected override void RemoveChildNode(MissionNode child)
    {
        if (child is Substage substage)
        {
            _substages.Remove(substage);
        }
    }

    protected override bool CanContain(MissionNodeType childType)
    {
        return childType == MissionNodeType.Substage;
    }
}
