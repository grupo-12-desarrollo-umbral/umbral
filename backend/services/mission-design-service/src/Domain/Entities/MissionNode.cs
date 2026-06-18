using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.Entities;

/// <summary>
/// Composite component of a <see cref="Mission"/> authoring tree. Concrete nodes
/// are <see cref="Stage"/>, <see cref="Substage"/>, and <see cref="Clue"/>. The
/// node, not a handler, owns the rule for which children it may contain so the
/// hierarchy <c>Stage -&gt; Substage -&gt; Clue</c> stays coherent.
/// </summary>
public abstract class MissionNode : BaseEntity
{
    protected MissionNode()
    {
        Title = string.Empty;
    }

    protected MissionNode(string title, int sequenceOrder)
    {
        Title = ValidateTitle(title);
        SequenceOrder = ValidateSequenceOrder(sequenceOrder);
    }

    public string Title { get; private set; }

    public int SequenceOrder { get; private set; }

    public abstract MissionNodeType NodeType { get; }

    /// <summary>
    /// The node's children, held in a strongly typed backing list on each concrete
    /// node so EF Core can map the owned collection by its element type. The base
    /// Composite operations project over this sequence rather than owning storage.
    /// </summary>
    protected abstract IEnumerable<MissionNode> ChildNodes { get; }

    public IReadOnlyList<MissionNode> Children =>
        ChildNodes.OrderBy(child => child.SequenceOrder).ToList().AsReadOnly();

    public MissionNode AddChild(MissionNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        EnsureChildIsAllowed(child.NodeType);

        AddChildNode(child);
        return child;
    }

    public void RemoveChild(MissionNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        RemoveChildNode(child);
    }

    public void Rename(string title, int sequenceOrder)
    {
        Title = ValidateTitle(title);
        SequenceOrder = ValidateSequenceOrder(sequenceOrder);
    }

    /// <summary>
    /// Recursively walks this node and its descendants. Used by readiness checks so
    /// traversal stays inside the Composite instead of leaking into handlers.
    /// </summary>
    public IEnumerable<MissionNode> Descendants()
    {
        foreach (var child in ChildNodes)
        {
            yield return child;

            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Append a type-validated child to the concrete backing list.</summary>
    protected abstract void AddChildNode(MissionNode child);

    /// <summary>Remove a child from the concrete backing list, if present.</summary>
    protected abstract void RemoveChildNode(MissionNode child);

    protected abstract bool CanContain(MissionNodeType childType);

    private void EnsureChildIsAllowed(MissionNodeType childType)
    {
        if (!CanContain(childType))
        {
            throw new InvalidMissionNodeChildException(NodeType, childType);
        }
    }

    private static string ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new MissionNodeTitleRequiredException();
        }

        return title.Trim();
    }

    private static int ValidateSequenceOrder(int sequenceOrder)
    {
        if (sequenceOrder <= 0)
        {
            throw new MissionNodeSequenceOrderMustBePositiveException();
        }

        return sequenceOrder;
    }
}
