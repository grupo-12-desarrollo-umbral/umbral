namespace umbral_backend.Application.Dtos.Scores;

// Real-time payload pushed over the ScoringHub's "PenaltyApplied" method. Distinct from AppliedPenaltyDto
// (the REST response to the operator): this one is participant-facing and carries the routing identifiers
// (LiveSessionId/TeamId) the client needs to match the notification to its own team.
public sealed record PenaltyAppliedNotificationDto(
    Guid LiveSessionId,
    Guid TeamId,
    Guid PenaltyId,
    Guid ScoreEntryId,
    int DeductionMagnitude,
    string Reason,
    DateTimeOffset AppliedAt);
