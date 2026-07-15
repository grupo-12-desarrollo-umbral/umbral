using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="AnswerRegisteredEvent"/> domain fact onto the RabbitMQ transport through
/// MassTransit's <see cref="IPublishEndpoint"/>. Dispatched by <c>OutboxDomainEventDispatcher</c> from
/// the interceptor's pre-commit phase: under the bus outbox this <c>Publish</c> is a local
/// OutboxMessage insert that commits atomically with the accepted-answer write and is drained to the
/// broker asynchronously (AC #4) — never blocking the hot path. A publish failure is a DbContext fault,
/// so it is logged and rethrown to roll the transaction back rather than silently dropped.
/// Correctness/score ride the contract for downstream scoring.
/// </summary>
public sealed class PublishAnswerRegisteredIntegrationEventHandler
{
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
            await _publishEndpoint.Publish(
                new AnswerRegisteredIntegrationEvent(
                    notification.LiveSessionId,
                    notification.TeamId,
                    notification.ReferenceTeamId,
                    notification.TeamDisplayName,
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
                "Failed to enqueue AnswerRegisteredIntegrationEvent for session {LiveSessionId} team {TeamId} question {QuestionSequenceOrder}.",
                notification.LiveSessionId,
                notification.TeamId,
                notification.QuestionSequenceOrder);
            throw;
        }
    }
}
