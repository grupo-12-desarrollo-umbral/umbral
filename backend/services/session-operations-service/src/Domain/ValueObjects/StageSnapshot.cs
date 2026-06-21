using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class StageSnapshot : ValueObject
{
    private readonly List<SubstageSnapshot> _substageSnapshots = [];

    private StageSnapshot()
    {
        StageSnapshotId = Guid.Empty;
        Title = string.Empty;
    }

    private StageSnapshot(
        Guid stageSnapshotId,
        string title,
        int sequenceOrder,
        IEnumerable<SubstageSnapshot> substageSnapshots)
    {
        var normalizedSubstages = substageSnapshots?.ToArray() ?? [];
        if (stageSnapshotId == Guid.Empty)
        {
            throw new StageSnapshotIdRequiredException();
        }

        if (normalizedSubstages.Length == 0)
        {
            throw new StageSnapshotMustContainSubstagesException();
        }

        StageSnapshotId = stageSnapshotId;
        Title = title.Trim();
        SequenceOrder = sequenceOrder;

        EnsureStrictSubstageOrder(normalizedSubstages);
        _substageSnapshots.AddRange(normalizedSubstages);
    }

    public Guid StageSnapshotId { get; }

    public string Title { get; }

    public int SequenceOrder { get; }

    public IReadOnlyCollection<SubstageSnapshot> SubstageSnapshots => _substageSnapshots.AsReadOnly();

    public static StageSnapshot Create(
        string title,
        int sequenceOrder,
        IEnumerable<SubstageSnapshot> substageSnapshots)
    {
        return new StageSnapshot(Guid.NewGuid(), title, sequenceOrder, substageSnapshots);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StageSnapshotId;
        yield return Title;
        yield return SequenceOrder;

        foreach (var substageSnapshot in _substageSnapshots)
        {
            yield return substageSnapshot;
        }
    }

    private static void EnsureStrictSubstageOrder(IEnumerable<SubstageSnapshot> substageSnapshots)
    {
        var ordered = substageSnapshots
            .OrderBy(substage => substage.SequenceOrder)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index].SequenceOrder != index + 1)
            {
                throw new MissionRuntimeSnapshotSubstageOrderInvalidException();
            }
        }
    }
}
