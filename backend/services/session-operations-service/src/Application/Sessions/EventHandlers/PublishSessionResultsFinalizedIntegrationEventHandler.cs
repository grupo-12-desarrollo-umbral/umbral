using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the final-results fact — <see cref="SessionStateChangedEvent"/> reaching
/// <see cref="SessionState.Finished"/> — straight through MassTransit's
/// <see cref="IPublishEndpoint"/>. Only a transition to Finished publishes (State-gated in the
/// domain); any other target state is a no-op. Dispatched by <c>OutboxDomainEventDispatcher</c> from
/// the interceptor's pre-commit phase: under the bus outbox this <c>Publish</c> is a local
/// OutboxMessage insert that commits atomically with the Finished transition and drains to the broker
/// asynchronously. A publish failure is a DbContext fault, so it is logged and rethrown to roll the
/// transaction back rather than swallowed.
/// </summary>
public sealed class PublishSessionResultsFinalizedIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishSessionResultsFinalizedIntegrationEventHandler> _logger;

    public PublishSessionResultsFinalizedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishSessionResultsFinalizedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(SessionStateChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.CurrentState != SessionState.Finished)
        {
            return;
        }

        try
        {
            await _publishEndpoint.Publish(
                new SessionResultsFinalizedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.ChangedAt),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to enqueue SessionResultsFinalizedIntegrationEvent for session {LiveSessionId}.",
                notification.LiveSessionId);
            throw;
        }
    }
}
