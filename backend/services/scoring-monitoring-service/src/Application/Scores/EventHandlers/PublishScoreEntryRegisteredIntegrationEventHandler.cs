using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Scores.EventHandlers;

public sealed class PublishScoreEntryRegisteredIntegrationEventHandler : INotificationHandler<ScoreEntryRegistered>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishScoreEntryRegisteredIntegrationEventHandler> _logger;

    public PublishScoreEntryRegisteredIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishScoreEntryRegisteredIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(ScoreEntryRegistered notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
                new ScoreEntryRegisteredIntegrationEvent(
                    notification.ScoreEntryId,
                    notification.LiveSessionId,
                    notification.TeamId,
                    notification.EntryType,
                    notification.ReasonCode,
                    notification.ScoreValue,
                    notification.RecordedAt,
                    notification.SourceEntityType,
                    notification.SourceEntityId,
                    notification.RecordedByUserId),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish ScoreEntryRegisteredIntegrationEvent for session {LiveSessionId} team {TeamId}.",
                notification.LiveSessionId,
                notification.TeamId);
        }
    }
}
