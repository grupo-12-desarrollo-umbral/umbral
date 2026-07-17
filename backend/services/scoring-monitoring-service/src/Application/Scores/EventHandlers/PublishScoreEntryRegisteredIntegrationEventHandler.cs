using MassTransit;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Scores.EventHandlers;

public sealed class PublishScoreEntryRegisteredIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;

    public PublishScoreEntryRegisteredIntegrationEventHandler(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task Handle(ScoreEntryRegistered notification, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(
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
}
