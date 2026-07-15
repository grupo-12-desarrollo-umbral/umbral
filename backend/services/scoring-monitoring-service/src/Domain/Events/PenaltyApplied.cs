namespace umbral_backend.Domain.Events;

public sealed class PenaltyApplied : BaseEvent
{
    public PenaltyApplied(
        Guid penaltyId,
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid teamId,
        int deductionMagnitude,
        DateTimeOffset appliedAt,
        Guid appliedByUserId,
        string reason)
    {
        PenaltyId = penaltyId;
        ScoreEntryId = scoreEntryId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        DeductionMagnitude = deductionMagnitude;
        AppliedAt = appliedAt;
        AppliedByUserId = appliedByUserId;
        Reason = reason;
    }

    public Guid PenaltyId { get; }

    public Guid ScoreEntryId { get; }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    public int DeductionMagnitude { get; }

    public DateTimeOffset AppliedAt { get; }

    public Guid AppliedByUserId { get; }

    public string Reason { get; }
}
