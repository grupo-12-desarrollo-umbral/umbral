using MassTransit;

namespace umbral_backend.Application.Scores.Common;

[EntityName("scoring-penalty-applied")]
public sealed record PenaltyAppliedIntegrationEvent(
    Guid PenaltyId,
    Guid ScoreEntryId,
    Guid LiveSessionId,
    Guid TeamId,
    int DeductionMagnitude,
    string Reason,
    DateTimeOffset AppliedAt,
    Guid AppliedByUserId);
