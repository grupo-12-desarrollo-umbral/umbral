using MassTransit;

namespace umbral_backend.Application.SessionEvents.Common;

[MessageUrn("umbral_backend.Application.Sessions.Common:SessionResultsFinalizedIntegrationEvent")]
[EntityName("session-results-finalized")]
public sealed record SessionResultsFinalizedIntegrationEvent(
    Guid LiveSessionId,
    DateTimeOffset FinishedAt);
