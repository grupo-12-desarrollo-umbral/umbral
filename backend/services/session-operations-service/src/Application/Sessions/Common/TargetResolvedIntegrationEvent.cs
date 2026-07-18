using MassTransit;

namespace umbral_backend.Application.Sessions.Common;

[EntityName("session-target-resolved")]
public sealed record TargetResolvedIntegrationEvent(
    Guid LiveSessionId,
    Guid TeamId,
    Guid ReferenceTeamId,
    string TeamDisplayName,
    Guid EvidenceSubmissionId,
    Guid ActiveSubstageId,
    Guid TargetSnapshotId,
    int ScoreValue,
    int DifficultyFactor,
    DateTimeOffset ResolvedAt);
