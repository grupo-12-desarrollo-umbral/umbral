using MassTransit;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Scores.Common;

[EntityName("scoring-score-entry-registered")]
public sealed record ScoreEntryRegisteredIntegrationEvent(
    Guid ScoreEntryId,
    Guid LiveSessionId,
    Guid TeamId,
    ScoreEntryType EntryType,
    string ReasonCode,
    int ScoreValue,
    DateTimeOffset RecordedAt,
    ScoreSourceType SourceEntityType,
    Guid SourceEntityId,
    int? RecordedByUserId);
