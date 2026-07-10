using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="AnswerRegisteredEvent"/> domain fact onto the RabbitMQ transport through
/// the existing HU-33B <see cref="IIntegrationEventPublisher"/> seam. Domain events are dispatched
/// inside SaveChanges = after transactional success, so this fires only once the accepted answer is
/// persisted (AC #4). Broker/publish failures are logged and swallowed so the runtime never faults
/// on a transport problem (D-3, AC #6). Correctness/score ride the contract for downstream scoring.
/// </summary>
public sealed class PublishAnswerRegisteredIntegrationEventHandler : INotificationHandler<AnswerRegisteredEvent>
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<PublishAnswerRegisteredIntegrationEventHandler> _logger;

    public PublishAnswerRegisteredIntegrationEventHandler(
        IIntegrationEventPublisher publisher,
        ILogger<PublishAnswerRegisteredIntegrationEventHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(AnswerRegisteredEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(
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
                cancellationToken);
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
