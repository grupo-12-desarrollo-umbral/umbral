using MassTransit;

namespace umbral_backend.Application.Scores.Common;

// See AnswerRegisteredIntegrationEvent / TargetResolvedIntegrationEvent for the full rationale: the
// publisher (session-operations-service) declares this contract in
// `umbral_backend.Application.Sessions.Common`, so its message-type URN uses that namespace. Without
// pinning the URN here, MassTransit cannot match the deserialized message to this consumer and moves
// every event to the `_skipped` queue (operator never authorized to penalize). Keep in sync with the
// publisher. MassTransit prepends the `urn:message:` prefix itself, so supply only the namespace:type
// suffix.
[MessageUrn("umbral_backend.Application.Sessions.Common:LiveSessionOperatorAssignedIntegrationEvent")]
[EntityName("session-operator-assigned")]
public sealed record LiveSessionOperatorAssignedIntegrationEvent(
    Guid LiveSessionId,
    Guid AssignedOperatorUserId,
    DateTimeOffset AssignedAt);
