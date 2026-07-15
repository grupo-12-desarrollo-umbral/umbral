using MassTransit;

namespace umbral_backend.Application.Scores.Common;

[EntityName("session-operator-assigned")]
public sealed record LiveSessionOperatorAssignedIntegrationEvent(
    Guid LiveSessionId,
    Guid AssignedOperatorUserId,
    DateTimeOffset AssignedAt);
