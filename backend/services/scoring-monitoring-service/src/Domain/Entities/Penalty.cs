using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Domain.Entities;

public sealed class Penalty : BaseEntity
{
    private Penalty()
    {
        PenaltyId = Guid.Empty;
        PenaltyReason = null!;
    }

    private Penalty(
        Guid penaltyId,
        Guid scoreEntryId,
        PenaltyReason penaltyReason,
        DateTimeOffset appliedAt,
        Guid appliedByUserId)
    {
        PenaltyId = penaltyId;
        ScoreEntryId = scoreEntryId;
        PenaltyReason = penaltyReason;
        AppliedAt = appliedAt;
        AppliedByUserId = appliedByUserId;
    }

    public Guid PenaltyId { get; private set; }

    public Guid ScoreEntryId { get; private set; }

    public PenaltyReason PenaltyReason { get; private set; }

    public DateTimeOffset AppliedAt { get; private set; }

    public Guid AppliedByUserId { get; private set; }

    public static Penalty Create(Guid scoreEntryId, string reason, Guid appliedByUserId)
    {
        var penaltyReason = PenaltyReason.Create(reason);

        return new Penalty(
            Guid.NewGuid(),
            scoreEntryId,
            penaltyReason,
            DateTimeOffset.UtcNow,
            appliedByUserId);
    }
}
