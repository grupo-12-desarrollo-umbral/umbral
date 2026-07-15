using MassTransit;

namespace umbral_backend.Application.SessionEvents.Common;

[MessageUrn("umbral_backend.Application.Sessions.Common:QuestionClosedIntegrationEvent")]
[EntityName("session-question-closed")]
public sealed record QuestionClosedIntegrationEvent(
    Guid LiveSessionId,
    int QuestionIndex,
    DateTimeOffset ClosedAt);
