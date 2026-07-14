using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Scores.Commands.RecordScoreEntry;

public sealed record RecordScoreEntryCommand(
    Guid LiveSessionId,
    Guid TeamId,
    string ReasonCode,
    int ScoreValue,
    DateTimeOffset RecordedAt,
    ScoreSourceType SourceEntityType,
    Guid SourceEntityId,
    int? RecordedByUserId = null) : IRequest;
