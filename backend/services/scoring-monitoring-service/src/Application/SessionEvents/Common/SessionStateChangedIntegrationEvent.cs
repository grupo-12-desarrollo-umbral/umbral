using MassTransit;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.SessionEvents.Common;

[MessageUrn("umbral_backend.Application.Sessions.Common:SessionStateChangedIntegrationEvent")]
[EntityName("session-state-changed")]
public sealed record SessionStateChangedIntegrationEvent(
    Guid LiveSessionId,
    SessionState PreviousState,
    SessionState CurrentState,
    DateTimeOffset ChangedAt,
    int? ResponsibleUserId,
    string? Reason);
