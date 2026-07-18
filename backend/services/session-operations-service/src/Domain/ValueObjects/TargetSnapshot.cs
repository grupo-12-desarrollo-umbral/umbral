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
        int score,
        int difficultyFactor,
        double latitude,
        double longitude,
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

        if (score <= 0)
        {
            throw new TargetSnapshotScoreMustBePositiveException();
        }

        if (difficultyFactor <= 0)
        {
            throw new TargetSnapshotDifficultyFactorMustBePositiveException();
        }

        TargetSnapshotId = targetSnapshotId;
        SubstageSnapshotId = substageSnapshotId;
        Name = name.Trim();
        QrCode = qrCode.Trim();
        SequenceOrder = sequenceOrder;
        IsActive = isActive;
        Score = score;
        DifficultyFactor = difficultyFactor;
        Latitude = latitude;
        Longitude = longitude;
        ClueText = string.IsNullOrWhiteSpace(clueText) ? null : clueText.Trim();
        ClueVisibilityPolicy = string.IsNullOrWhiteSpace(clueVisibilityPolicy) ? null : clueVisibilityPolicy.Trim();
    }

    public Guid TargetSnapshotId { get; }

    public Guid SubstageSnapshotId { get; }

    public string Name { get; }

    public string QrCode { get; }

    public int SequenceOrder { get; }

    public bool IsActive { get; }

    public int Score { get; }

    // Mission difficulty multiplier (1/2/3) that produced Score (= base 50 × factor). Carried on the
    // snapshot so target resolution can hand the real difficulty factor to scoring, where the
    // "puntaje según dificultad" Strategy computes the award, rather than passing a pre-weighted value.
    public int DifficultyFactor { get; }

    // Display/context metadata copied immutably from the mission target. QR validation, not
    // these coordinates, remains the source of truth for target resolution.
    public double Latitude { get; }

    public double Longitude { get; }

    public string? ClueText { get; }

    public string? ClueVisibilityPolicy { get; }

    public static TargetSnapshot Create(
        Guid substageSnapshotId,
        string name,
        string qrCode,
        int sequenceOrder,
        bool isActive,
        int score,
        double latitude,
        double longitude,
        string? clueText,
        string? clueVisibilityPolicy,
        // Optional/trailing with a base-weight default so the many callers that don't exercise
        // difficulty stay unchanged; session creation passes the mission's real factor (1/2/3).
        int difficultyFactor = 1)
    {
        return new TargetSnapshot(
            Guid.NewGuid(),
            substageSnapshotId,
            name,
            qrCode,
            sequenceOrder,
            isActive,
            score,
            difficultyFactor,
            latitude,
            longitude,
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
        yield return Score;
        yield return DifficultyFactor;
        yield return Latitude;
        yield return Longitude;
        yield return ClueText;
        yield return ClueVisibilityPolicy;
    }
}
