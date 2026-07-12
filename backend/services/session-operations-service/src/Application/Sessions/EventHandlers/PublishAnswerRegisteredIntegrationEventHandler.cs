using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="AnswerRegisteredEvent"/> domain fact onto the RabbitMQ transport through
/// MassTransit's <see cref="IPublishEndpoint"/>. Domain events are dispatched inside SaveChanges =
/// after transactional success, so this fires only once the accepted answer is persisted (AC #4).
/// Broker/publish failures are logged and swallowed so the runtime never faults on a transport
/// problem (D-3, AC #6). Correctness/score ride the contract for downstream scoring.
/// </summary>
public sealed class PublishAnswerRegisteredIntegrationEventHandler : INotificationHandler<AnswerRegisteredEvent>
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishAnswerRegisteredIntegrationEventHandler> _logger;

    public PublishAnswerRegisteredIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishAnswerRegisteredIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(AnswerRegisteredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishTimeout.CancelAfter(PublishTimeout);
            await _publishEndpoint.Publish(
                new AnswerRegisteredIntegrationEvent(
                    notification.LiveSessionId,
                    notification.TeamId,
                    notification.EvidenceSubmissionId,
                    notification.ActiveSubstageId,
                    notification.QuestionSequenceOrder,
                    notification.SelectedOptionSequenceOrder,
                    notification.IsCorrect,
                    notification.ScoreValue,
                    notification.SubmittedAt),
                publishTimeout.Token);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish AnswerRegisteredIntegrationEvent for session {LiveSessionId} team {TeamId} question {QuestionSequenceOrder}.",
                notification.LiveSessionId,
                notification.TeamId,
                notification.QuestionSequenceOrder);
        }
    }
}
