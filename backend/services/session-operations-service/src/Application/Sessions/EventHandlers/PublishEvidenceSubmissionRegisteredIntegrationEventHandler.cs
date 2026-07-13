using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Maps the generic evidence-registration domain fact to its RabbitMQ contract. Under MassTransit's
/// bus outbox this publish inserts an OutboxMessage before the business transaction commits; failures
/// are rethrown so the evidence write and event capture roll back together.
/// </summary>
public sealed class PublishEvidenceSubmissionRegisteredIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishEvidenceSubmissionRegisteredIntegrationEventHandler> _logger;

    public PublishEvidenceSubmissionRegisteredIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishEvidenceSubmissionRegisteredIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(
        EvidenceSubmissionRegisteredEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publishEndpoint.Publish(
                new EvidenceSubmissionRegisteredIntegrationEvent(
                    notification.LiveSessionId,
                    notification.TeamId,
                    notification.EvidenceSubmissionId,
                    notification.ActiveSubstageId,
                    notification.SubmissionType,
                    notification.SubmittedAt,
                    notification.ValidationState),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to enqueue EvidenceSubmissionRegisteredIntegrationEvent for session {LiveSessionId} team {TeamId} submission {EvidenceSubmissionId}.",
                notification.LiveSessionId,
                notification.TeamId,
                notification.EvidenceSubmissionId);
            throw;
        }
    }
}
