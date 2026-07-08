using Microsoft.Extensions.Logging;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="QuestionClosedEvent"/> domain fact to the integration-event transport
/// (HU-33B). Dispatched inside SaveChanges = after transactional success; broker/publish failures
/// are logged and swallowed so the runtime never faults on a publish problem (D-3, AC #6).
/// </summary>
public sealed class PublishQuestionClosedIntegrationEventHandler : INotificationHandler<QuestionClosedEvent>
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly ILogger<PublishQuestionClosedIntegrationEventHandler> _logger;

    public PublishQuestionClosedIntegrationEventHandler(
        IIntegrationEventPublisher publisher,
        ILogger<PublishQuestionClosedIntegrationEventHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(QuestionClosedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.PublishAsync(
                new QuestionClosedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.QuestionIndex,
                    notification.ClosedAt),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to publish QuestionClosedIntegrationEvent for session {LiveSessionId} question {QuestionIndex}.",
                notification.LiveSessionId,
                notification.QuestionIndex);
        }
    }
}
