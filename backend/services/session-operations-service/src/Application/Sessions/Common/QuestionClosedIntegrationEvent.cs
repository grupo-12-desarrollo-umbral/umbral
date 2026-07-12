using MassTransit;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// History-correlation fact published when a trivia question closes (HU-33B). Published over
/// MassTransit/RabbitMQ (#164) to the <c>session-question-closed</c> exchange named via
/// <see cref="EntityNameAttribute"/>. Carries only correlation data — no score/ranking, which is
/// computed downstream by ScoringMonitoring (D-1).
/// </summary>
[EntityName("session-question-closed")]
public sealed record QuestionClosedIntegrationEvent(
    Guid LiveSessionId,
    int QuestionIndex,
    DateTimeOffset ClosedAt);
