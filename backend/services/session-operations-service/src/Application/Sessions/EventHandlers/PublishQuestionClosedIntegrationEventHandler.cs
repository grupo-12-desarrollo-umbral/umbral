using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="QuestionClosedEvent"/> domain fact to RabbitMQ, publishing the
/// integration event straight through MassTransit's <see cref="IPublishEndpoint"/> (#164 — the
/// idiomatic path, no hand-rolled publisher seam). Dispatched by <c>OutboxDomainEventDispatcher</c>
/// from the interceptor's pre-commit phase: under the bus outbox this <c>Publish</c> is a local
/// OutboxMessage insert that commits atomically with the close and drains to the broker
/// asynchronously — a down broker no longer stalls the close/advance transition. A publish failure is
/// a DbContext fault, so it is logged and rethrown to roll the transaction back rather than swallowed.
/// </summary>
public sealed class PublishQuestionClosedIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishQuestionClosedIntegrationEventHandler> _logger;

    public PublishQuestionClosedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishQuestionClosedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(QuestionClosedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
                new QuestionClosedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.QuestionIndex,
                    notification.ClosedAt),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to enqueue QuestionClosedIntegrationEvent for session {LiveSessionId} question {QuestionIndex}.",
                notification.LiveSessionId,
                notification.QuestionIndex);
            throw;
        }
    }
}
