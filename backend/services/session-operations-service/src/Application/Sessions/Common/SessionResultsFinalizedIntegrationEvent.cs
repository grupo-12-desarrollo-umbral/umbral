namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// History-correlation fact published when a session reaches Finished / final results
/// (HU-33B, routing key <c>session.results.finalized</c>). Carries only correlation data —
/// no score/ranking, which is computed downstream by ScoringMonitoring (D-1).
/// </summary>
public sealed record SessionResultsFinalizedIntegrationEvent(
    Guid LiveSessionId,
    DateTimeOffset FinishedAt);
