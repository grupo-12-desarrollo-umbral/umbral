using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Maps the evidence-accepted domain fact to its RabbitMQ contract. Under MassTransit's
/// bus outbox this publish inserts an OutboxMessage before the business transaction commits; failures
/// are rethrown so the evidence write and event capture roll back together.
/// </summary>
public sealed class PublishEvidenceSubmissionAcceptedIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishEvidenceSubmissionAcceptedIntegrationEventHandler> _logger;

    public PublishEvidenceSubmissionAcceptedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishEvidenceSubmissionAcceptedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(
        EvidenceSubmissionAcceptedEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
                new EvidenceSubmissionAcceptedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.TeamId,
                    notification.EvidenceSubmissionId,
                    notification.ActiveSubstageId,
                    notification.SubmissionType,
                    notification.SubmittedAt,
                    notification.ResolvedAt),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to enqueue EvidenceSubmissionAcceptedIntegrationEvent for session {LiveSessionId} team {TeamId} submission {EvidenceSubmissionId}.",
                notification.LiveSessionId,
                notification.TeamId,
                notification.EvidenceSubmissionId);
            throw;
        }
    }
}
