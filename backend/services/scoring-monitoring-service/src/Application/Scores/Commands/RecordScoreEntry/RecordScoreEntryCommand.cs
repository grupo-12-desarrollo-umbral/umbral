using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Scores.Commands.RecordScoreEntry;

public sealed record RecordScoreEntryCommand(
    Guid LiveSessionId,
    Guid TeamId,
    string TeamDisplayName,
    string ReasonCode,
    int ScoreValue,
    DateTimeOffset RecordedAt,
    ScoreSourceType SourceEntityType,
    Guid SourceEntityId,
    int? RecordedByUserId = null,
    // Mission difficulty multiplier for difficulty-weighted sources (treasure-hunt targets). Defaults
    // to the base weight for sources that are not difficulty-weighted (trivia), whose policy ignores it.
    int DifficultyFactor = 1) : IRequest;
