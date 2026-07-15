using MassTransit;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Operator-assignment fact published to RabbitMQ after transactional success on the
/// <c>session-operator-assigned</c> exchange named via <see cref="EntityNameAttribute"/>, consumed
/// downstream by ScoringMonitoring to fill its <c>session_operator_assignments</c> projection so the
/// assigned operator is authorized to apply penalties (HU-38). This is the publisher side of the
/// contract; the consumer copy in ScoringMonitoring pins its <c>MessageUrn</c> to THIS namespace, so
/// keep the record shape and namespace in sync. <see cref="AssignedOperatorUserId"/> is the operator's
/// external identity id (Keycloak sub) — the axis scoring authorizes on — not the internal numeric
/// user id the aggregate persists.
/// </summary>
[EntityName("session-operator-assigned")]
public sealed record LiveSessionOperatorAssignedIntegrationEvent(
    Guid LiveSessionId,
    Guid AssignedOperatorUserId,
    DateTimeOffset AssignedAt);
