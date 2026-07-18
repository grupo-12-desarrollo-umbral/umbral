namespace umbral_backend.Domain.Events;

// Raised alongside ScoreEntryRegistered when an operator applies a penalty. Unlike the ledger fact
// (which drives ranking recalculation), this event exists to push an explicit penalty notification to
// participants: the ranking snapshot alone cannot convey a penalty because the displayed team total is
// floored at zero (see Ranking.Refresh), so a penalty against a low-scoring team leaves the snapshot
// unchanged and a score-decrease heuristic would miss it. It carries the true, unclamped deduction
// magnitude so the client can always name the penalty regardless of the resulting standing.
public sealed class PenaltyApplied : BaseEvent
{
    public PenaltyApplied(
        Guid penaltyId,
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid teamId,
        int deductionMagnitude,
        string reason,
        DateTimeOffset appliedAt)
    {
        PenaltyId = penaltyId;
        ScoreEntryId = scoreEntryId;
        LiveSessionId = liveSessionId;
        TeamId = teamId;
        DeductionMagnitude = deductionMagnitude;
        Reason = reason;
        AppliedAt = appliedAt;
    }

    public Guid PenaltyId { get; }

    public Guid ScoreEntryId { get; }

    public Guid LiveSessionId { get; }

    public Guid TeamId { get; }

    // The unclamped magnitude of the deduction (a positive number). ScoreValue is always non-negative;
    // the fold in Ranking.Refresh is what negates it. The client shows this verbatim ("dropped by N").
    public int DeductionMagnitude { get; }

    public string Reason { get; }

    public DateTimeOffset AppliedAt { get; }
}
