namespace umbral_backend.Domain.ValueObjects;

// Player-facing clue guidance projected onto a team board. A clue is either target-associated
// (treasure hunt — carries TargetSnapshotId/TargetName) or substage-scoped (trivia — has no target,
// so both are null). Guidance only: it never advances a target or substage (#145).
public sealed class VisibleClue : ValueObject
{
    private VisibleClue(Guid? targetSnapshotId, string clueText, string? targetName)
    {
        TargetSnapshotId = targetSnapshotId;
        ClueText = clueText;
        TargetName = targetName;
    }

    public Guid? TargetSnapshotId { get; }

    public string ClueText { get; }

    public string? TargetName { get; }

    public static VisibleClue Create(Guid targetSnapshotId, string clueText, string targetName)
    {
        return new VisibleClue(targetSnapshotId, clueText, targetName);
    }

    // Substage-scoped clue with no owning target (trivia substages).
    public static VisibleClue CreateForSubstage(string clueText)
    {
        return new VisibleClue(null, clueText, null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetSnapshotId;
        yield return ClueText;
        yield return TargetName;
    }
}
