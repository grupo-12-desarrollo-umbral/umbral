using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

public sealed class PublishTargetResolvedIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishTargetResolvedIntegrationEventHandler> _logger;

    public PublishTargetResolvedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishTargetResolvedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(TargetResolvedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(new TargetResolvedIntegrationEvent(
                notification.LiveSessionId,
                notification.TeamId,
                notification.EvidenceSubmissionId,
                notification.ActiveSubstageId,
                notification.TargetSnapshotId,
                notification.ScoreValue,
                notification.ResolvedAt), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed to enqueue TargetResolvedIntegrationEvent for session {LiveSessionId} team {TeamId} target {TargetSnapshotId}.",
                notification.LiveSessionId,
                notification.TeamId,
                notification.TargetSnapshotId);
            throw;
        }
    }
}
