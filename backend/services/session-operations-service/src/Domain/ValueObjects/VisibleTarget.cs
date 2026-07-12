namespace umbral_backend.Domain.ValueObjects;

/// <summary>
/// An active treasure-hunt target surfaced to a participant's runtime board so the mobile app
/// can render it on a map. Coordinates are display/context metadata copied from the immutable
/// <see cref="TargetSnapshot"/>; QR validation remains the source of truth for resolution.
/// </summary>
public sealed class VisibleTarget : ValueObject
{
    private VisibleTarget(Guid targetSnapshotId, string name, int sequenceOrder, double latitude, double longitude)
    {
        TargetSnapshotId = targetSnapshotId;
        Name = name;
        SequenceOrder = sequenceOrder;
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid TargetSnapshotId { get; }

    public string Name { get; }

    public int SequenceOrder { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public static VisibleTarget Create(Guid targetSnapshotId, string name, int sequenceOrder, double latitude, double longitude)
    {
        return new VisibleTarget(targetSnapshotId, name, sequenceOrder, latitude, longitude);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetSnapshotId;
        yield return Name;
        yield return SequenceOrder;
        yield return Latitude;
        yield return Longitude;
    }
}
