namespace umbral_backend.Domain.ValueObjects;

public sealed class VisibleClue : ValueObject
{
    private VisibleClue(Guid targetSnapshotId, string clueText, string targetName)
    {
        TargetSnapshotId = targetSnapshotId;
        ClueText = clueText;
        TargetName = targetName;
    }

    public Guid TargetSnapshotId { get; }

    public string ClueText { get; }

    public string TargetName { get; }

    public static VisibleClue Create(Guid targetSnapshotId, string clueText, string targetName)
    {
        return new VisibleClue(targetSnapshotId, clueText, targetName);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetSnapshotId;
        yield return ClueText;
        yield return TargetName;
    }
}
