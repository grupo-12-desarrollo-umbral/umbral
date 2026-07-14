using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

// Substage-scoped clue captured at session creation (#145). A trivia substage has no targets, so a
// clue authored under it reaches the live session only through this snapshot — the per-target clue on
// TargetSnapshot remains the path for treasure-hunt clues. VisibilityPolicy mirrors the mission-design
// ClueVisibilityPolicy name; the runtime honors it when projecting visible clues.
public sealed class ClueSnapshot : ValueObject
{
    // Mission-design's ClueVisibilityPolicy.VisibleWhenSubstageStarts, serialized by name. Kept as a
    // string constant because SessionOperations does not own the authoring enum.
    public const string VisibleWhenSubstageStartsPolicy = "VisibleWhenSubstageStarts";
    public const string HiddenUntilOperatorReleasePolicy = "HiddenUntilOperatorRelease";

    private ClueSnapshot()
    {
        ClueSnapshotId = Guid.Empty;
        SubstageSnapshotId = Guid.Empty;
        Text = string.Empty;
        VisibilityPolicy = string.Empty;
    }

    private ClueSnapshot(
        Guid clueSnapshotId,
        Guid substageSnapshotId,
        string text,
        string visibilityPolicy,
        int sequenceOrder)
    {
        if (clueSnapshotId == Guid.Empty)
        {
            throw new ClueSnapshotIdRequiredException();
        }

        if (substageSnapshotId == Guid.Empty)
        {
            throw new ClueSnapshotSubstageRequiredException();
        }

        ClueSnapshotId = clueSnapshotId;
        SubstageSnapshotId = substageSnapshotId;
        Text = (text ?? string.Empty).Trim();
        VisibilityPolicy = (visibilityPolicy ?? string.Empty).Trim();
        SequenceOrder = sequenceOrder;
    }

    public Guid ClueSnapshotId { get; }

    public Guid SubstageSnapshotId { get; }

    public string Text { get; }

    public string VisibilityPolicy { get; }

    public int SequenceOrder { get; }

    public bool IsVisibleWhenSubstageStarts =>
        string.Equals(VisibilityPolicy, VisibleWhenSubstageStartsPolicy, StringComparison.OrdinalIgnoreCase);

    public bool IsHiddenUntilOperatorRelease =>
        string.Equals(VisibilityPolicy, HiddenUntilOperatorReleasePolicy, StringComparison.OrdinalIgnoreCase);

    public static ClueSnapshot Create(
        Guid substageSnapshotId,
        string text,
        string visibilityPolicy,
        int sequenceOrder)
    {
        return new ClueSnapshot(Guid.NewGuid(), substageSnapshotId, text, visibilityPolicy, sequenceOrder);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ClueSnapshotId;
        yield return SubstageSnapshotId;
        yield return Text;
        yield return VisibilityPolicy;
        yield return SequenceOrder;
    }
}
