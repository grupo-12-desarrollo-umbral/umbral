namespace umbral_backend.Domain.ValueObjects;

// A hidden clue in the active substage that an operator may select for release. Treasure-hunt clues
// carry target context; target-less trivia clues carry their own snapshot identity.
public sealed class ReleasableClue : ValueObject
{
    private ReleasableClue(
        Guid? targetId,
        Guid? clueId,
        string? targetName,
        int sequenceOrder,
        string clueText)
    {
        TargetId = targetId;
        ClueId = clueId;
        TargetName = targetName;
        SequenceOrder = sequenceOrder;
        ClueText = clueText;
    }

    public Guid? TargetId { get; }

    public Guid? ClueId { get; }

    public string? TargetName { get; }

    public int SequenceOrder { get; }

    public string ClueText { get; }

    public static ReleasableClue ForTarget(
        Guid targetId,
        string targetName,
        int sequenceOrder,
        string clueText)
    {
        return new ReleasableClue(targetId, null, targetName, sequenceOrder, clueText);
    }

    public static ReleasableClue ForSubstageClue(Guid clueId, int sequenceOrder, string clueText)
    {
        return new ReleasableClue(null, clueId, null, sequenceOrder, clueText);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetId;
        yield return ClueId;
        yield return TargetName;
        yield return SequenceOrder;
        yield return ClueText;
    }
}
