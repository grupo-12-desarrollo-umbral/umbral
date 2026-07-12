using MassTransit;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// History-correlation fact published when a session reaches Finished / final results
/// (HU-33B). Published over MassTransit/RabbitMQ to the <c>session-results-finalized</c>
/// exchange named via <see cref="EntityNameAttribute"/>. Carries only correlation data — no
/// score/ranking, which is computed downstream by ScoringMonitoring (D-1).
/// </summary>
[EntityName("session-results-finalized")]
public sealed record SessionResultsFinalizedIntegrationEvent(
    Guid LiveSessionId,
    DateTimeOffset FinishedAt);
