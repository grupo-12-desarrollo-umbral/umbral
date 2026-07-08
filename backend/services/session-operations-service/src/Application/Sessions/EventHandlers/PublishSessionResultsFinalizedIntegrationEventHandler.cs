using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the final-results fact — <see cref="SessionStateChangedEvent"/> reaching
/// <see cref="SessionState.Finished"/> — to the integration-event transport (HU-33B). Only a
/// transition to Finished publishes (State-gated in the domain); any other target state is a no-op.
/// Broker/publish failures are logged and swallowed so the runtime never faults (D-3, AC #6).
/// </summary>
public sealed class PublishSessionResultsFinalizedIntegrationEventHandler : INotificationHandler<SessionStateChangedEvent>
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<PublishSessionResultsFinalizedIntegrationEventHandler> _logger;

    public PublishSessionResultsFinalizedIntegrationEventHandler(
        IIntegrationEventPublisher publisher,
        ILogger<PublishSessionResultsFinalizedIntegrationEventHandler> logger)
    {
        _publisher = publisher;
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
            await _publisher.PublishAsync(
                new SessionResultsFinalizedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.ChangedAt),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish SessionResultsFinalizedIntegrationEvent for session {LiveSessionId}.",
                notification.LiveSessionId);
        }
    }
}
