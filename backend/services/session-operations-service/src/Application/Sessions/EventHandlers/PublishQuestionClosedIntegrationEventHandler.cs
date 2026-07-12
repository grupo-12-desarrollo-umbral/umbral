using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="QuestionClosedEvent"/> domain fact to RabbitMQ, publishing the
/// integration event straight through MassTransit's <see cref="IPublishEndpoint"/> (#164 — the
/// idiomatic path, no hand-rolled publisher seam). Dispatched inside SaveChanges = after
/// transactional success; broker/publish failures are logged and swallowed so the runtime never
/// faults on a publish problem (D-3, AC #6). The publish is bounded by <see cref="PublishTimeout"/>
/// so a down/unreachable broker fails fast (MassTransit's Publish blocks under its retry policy
/// instead of throwing) — a broker outage bounds the post-commit stall to that timeout rather than
/// blocking indefinitely; the resulting cancellation is caught and swallowed like any other failure.
/// </summary>
public sealed class PublishQuestionClosedIntegrationEventHandler : INotificationHandler<QuestionClosedEvent>
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishQuestionClosedIntegrationEventHandler> _logger;

    public PublishQuestionClosedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishQuestionClosedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(QuestionClosedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishTimeout.CancelAfter(PublishTimeout);
            await _publishEndpoint.Publish(
                new QuestionClosedIntegrationEvent(
                    notification.LiveSessionId,
                    notification.QuestionIndex,
                    notification.ClosedAt),
                publishTimeout.Token);
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
