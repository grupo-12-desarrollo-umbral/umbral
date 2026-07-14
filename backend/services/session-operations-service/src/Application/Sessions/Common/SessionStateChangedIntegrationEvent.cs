using MassTransit;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.Sessions.Common;

/// <summary>
/// Auditable session-state transition fact published through MassTransit's transactional outbox.
/// </summary>
[EntityName("session-state-changed")]
public sealed record SessionStateChangedIntegrationEvent(
    Guid LiveSessionId,
    SessionState PreviousState,
    SessionState CurrentState,
    DateTimeOffset ChangedAt,
    int? ResponsibleUserId,
    string? Reason);
