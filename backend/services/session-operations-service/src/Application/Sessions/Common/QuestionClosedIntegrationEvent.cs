namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// History-correlation fact published when a trivia question closes (HU-33B, routing
/// key <c>session.question.closed</c>). Carries only correlation data — no score/ranking,
/// which is computed downstream by ScoringMonitoring (D-1).
/// </summary>
public sealed record QuestionClosedIntegrationEvent(
    Guid LiveSessionId,
    int QuestionIndex,
    DateTimeOffset ClosedAt);
