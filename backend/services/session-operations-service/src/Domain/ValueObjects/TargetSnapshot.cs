using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Domain.ValueObjects;

public sealed class TargetSnapshot : ValueObject
{
    private TargetSnapshot()
    {
        TargetSnapshotId = Guid.Empty;
        SubstageSnapshotId = Guid.Empty;
        Name = string.Empty;
        QrCode = string.Empty;
    }

    private TargetSnapshot(
        Guid targetSnapshotId,
        Guid substageSnapshotId,
        string name,
        string qrCode,
        int sequenceOrder,
        bool isActive,
        string? clueText,
        string? clueVisibilityPolicy)
    {
        if (targetSnapshotId == Guid.Empty)
        {
            throw new TargetSnapshotIdRequiredException();
        }

        if (substageSnapshotId == Guid.Empty)
        {
            throw new TargetSnapshotSubstageRequiredException();
        }

        TargetSnapshotId = targetSnapshotId;
        SubstageSnapshotId = substageSnapshotId;
        Name = name.Trim();
        QrCode = qrCode.Trim();
        SequenceOrder = sequenceOrder;
        IsActive = isActive;
        ClueText = string.IsNullOrWhiteSpace(clueText) ? null : clueText.Trim();
        ClueVisibilityPolicy = string.IsNullOrWhiteSpace(clueVisibilityPolicy) ? null : clueVisibilityPolicy.Trim();
    }

    public Guid TargetSnapshotId { get; }

    public Guid SubstageSnapshotId { get; }

    public string Name { get; }

    public string QrCode { get; }

    public int SequenceOrder { get; }

    public bool IsActive { get; }

    public string? ClueText { get; }

    public string? ClueVisibilityPolicy { get; }

    public static TargetSnapshot Create(
        Guid substageSnapshotId,
        string name,
        string qrCode,
        int sequenceOrder,
        bool isActive,
        string? clueText,
        string? clueVisibilityPolicy)
    {
        return new TargetSnapshot(
            Guid.NewGuid(),
            substageSnapshotId,
            name,
            qrCode,
            sequenceOrder,
            isActive,
            clueText,
            clueVisibilityPolicy);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetSnapshotId;
        yield return SubstageSnapshotId;
        yield return Name;
        yield return QrCode;
        yield return SequenceOrder;
        yield return IsActive;
        yield return ClueText;
        yield return ClueVisibilityPolicy;
    }
}
