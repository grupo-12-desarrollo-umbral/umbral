using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Maps every accepted state transition to its audit integration contract. The dispatcher invokes
/// this handler pre-commit, so MassTransit stores the publication in the transactional bus outbox
/// alongside the session update. An outbox insert failure must propagate and roll back the write.
/// </summary>
public sealed class PublishSessionStateChangedIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishSessionStateChangedIntegrationEventHandler> _logger;

    public PublishSessionStateChangedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishSessionStateChangedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(SessionStateChangedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
                new SessionStateChangedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.PreviousState,
                    notification.CurrentState,
                    notification.ChangedAt,
                    notification.ResponsibleUserExternalId,
                    notification.Reason),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to enqueue SessionStateChangedIntegrationEvent for session {LiveSessionId}.",
                notification.LiveSessionId);
            throw;
        }
    }
}
