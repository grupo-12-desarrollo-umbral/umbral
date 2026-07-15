namespace umbral_backend.Application.Dtos.Scores;

public sealed record AppliedPenaltyDto(
    Guid ScoreEntryId,
    Guid TeamId,
    int PenaltyAmount,
    string Reason,
    DateTimeOffset AppliedAt);
