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
/// domain); any other target state is a no-op. Broker/publish failures are logged and swallowed so
/// the runtime never faults (D-3, AC #6).
/// </summary>
public sealed class PublishSessionResultsFinalizedIntegrationEventHandler : INotificationHandler<SessionStateChangedEvent>
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

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
            using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishTimeout.CancelAfter(PublishTimeout);
            await _publishEndpoint.Publish(
                new SessionResultsFinalizedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.ChangedAt),
                publishTimeout.Token);
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
