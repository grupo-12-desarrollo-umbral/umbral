using umbral_backend.Domain.Enums;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// Optional player-facing guidance under a <see cref="Substage"/>. A clue is a
/// leaf node: it has no children. Clue visibility/release is guidance only and
/// never advances a target or substage.
/// </summary>
public sealed class Clue : MissionNode
{
    private Clue()
    {
        Text = string.Empty;
    }

    private Clue(string title, int sequenceOrder, string text, ClueVisibilityPolicy visibility)
        : base(title, sequenceOrder)
    {
        Text = text.Trim();
        Visibility = visibility;
    }

    public override MissionNodeType NodeType => MissionNodeType.Clue;

    public string Text { get; private set; }

    public ClueVisibilityPolicy Visibility { get; private set; }

    public static Clue Create(
        string title,
        int sequenceOrder,
        string text,
        ClueVisibilityPolicy visibility = ClueVisibilityPolicy.HiddenUntilOperatorRelease)
    {
        return new Clue(title, sequenceOrder, text ?? string.Empty, visibility);
    }

    protected override bool CanContain(MissionNodeType childType)
    {
        // A clue is a leaf node and may never contain child nodes.
        return false;
    }
}
