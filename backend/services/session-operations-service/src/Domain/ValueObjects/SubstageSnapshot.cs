using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class SubstageSnapshot : ValueObject
{
    private SubstageSnapshot()
    {
        SubstageSnapshotId = Guid.Empty;
        Title = string.Empty;
    }

    private SubstageSnapshot(
        Guid substageSnapshotId,
        string title,
        int sequenceOrder,
        SubstagePlayMode playMode)
    {
        if (substageSnapshotId == Guid.Empty)
        {
            throw new SubstageSnapshotIdRequiredException();
        }

        SubstageSnapshotId = substageSnapshotId;
        Title = title.Trim();
        SequenceOrder = sequenceOrder;
        PlayMode = playMode;
    }

    public Guid SubstageSnapshotId { get; }

    public string Title { get; }

    public int SequenceOrder { get; }

    public SubstagePlayMode PlayMode { get; }

    public static SubstageSnapshot CreateTreasureHunt(string title, int sequenceOrder)
    {
        return new SubstageSnapshot(Guid.NewGuid(), title, sequenceOrder, SubstagePlayMode.TreasureHunt);
    }

    public static SubstageSnapshot CreateTrivia(string title, int sequenceOrder)
    {
        return new SubstageSnapshot(Guid.NewGuid(), title, sequenceOrder, SubstagePlayMode.Trivia);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SubstageSnapshotId;
        yield return Title;
        yield return SequenceOrder;
        yield return PlayMode;
    }
}
