using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Scores.EventHandlers;

public sealed class PublishPenaltyAppliedIntegrationEventHandler : INotificationHandler<PenaltyApplied>
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishPenaltyAppliedIntegrationEventHandler> _logger;

    public PublishPenaltyAppliedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishPenaltyAppliedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(PenaltyApplied notification, CancellationToken cancellationToken)
    {
        try
        {
            using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishTimeout.CancelAfter(PublishTimeout);

            await _publishEndpoint.Publish(
                new PenaltyAppliedIntegrationEvent(
                    notification.PenaltyId,
                    notification.ScoreEntryId,
                    notification.LiveSessionId,
                    notification.TeamId,
                    notification.DeductionMagnitude,
                    notification.Reason,
                    notification.AppliedAt,
                    notification.AppliedByUserId),
                publishTimeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish PenaltyAppliedIntegrationEvent for session {LiveSessionId} team {TeamId}.",
                notification.LiveSessionId,
                notification.TeamId);
        }
    }
}
