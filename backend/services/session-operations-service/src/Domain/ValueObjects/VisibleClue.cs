namespace umbral_backend.Domain.ValueObjects;

// Player-facing clue guidance projected onto a team board. A clue is target-associated
// (treasure hunt — carries TargetSnapshotId/TargetName), substage-scoped (trivia), or an
// operator-authored operative clue carrying OperativeClueId. Guidance only: it never advances a
// target or substage (#145).
public sealed class VisibleClue : ValueObject
{
    private VisibleClue(
        Guid? targetSnapshotId,
        Guid? clueSnapshotId,
        Guid? operativeClueId,
        string clueText,
        string? targetName)
    {
        TargetSnapshotId = targetSnapshotId;
        ClueSnapshotId = clueSnapshotId;
        OperativeClueId = operativeClueId;
        ClueText = clueText;
        TargetName = targetName;
    }

    public Guid? TargetSnapshotId { get; }

    public Guid? ClueSnapshotId { get; }

    public Guid? OperativeClueId { get; }

    public string ClueText { get; }

    public string? TargetName { get; }

    public static VisibleClue Create(Guid targetSnapshotId, string clueText, string targetName)
    {
        return new VisibleClue(targetSnapshotId, null, null, clueText, targetName);
    }

    // Substage-scoped clue with no owning target (trivia substages).
    public static VisibleClue CreateForSubstage(Guid clueSnapshotId, string clueText)
    {
        return new VisibleClue(null, clueSnapshotId, null, clueText, null);
    }

    public static VisibleClue CreateForOperative(Guid operativeClueId, string clueText)
    {
        return new VisibleClue(null, null, operativeClueId, clueText, null);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetSnapshotId;
        yield return ClueSnapshotId;
        yield return OperativeClueId;
        yield return ClueText;
        yield return TargetName;
    }
}
