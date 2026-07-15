using MassTransit;
using Microsoft.Extensions.Logging;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="LiveSessionOperatorAssignedEvent"/> domain fact onto the RabbitMQ transport
/// through MassTransit's <see cref="IPublishEndpoint"/> so ScoringMonitoring can fill its
/// <c>session_operator_assignments</c> projection and authorize the assigned operator to apply
/// penalties (HU-38). Dispatched by <c>OutboxDomainEventDispatcher</c> from the interceptor's
/// pre-commit phase: under the bus outbox this <c>Publish</c> is a local OutboxMessage insert that
/// commits atomically with the assignment write and drains to the broker asynchronously. A publish
/// failure is a DbContext fault, so it is logged and rethrown to roll the transaction back rather than
/// silently dropped.
/// <para>
/// The scoring projection keys on the operator's external identity id (Keycloak sub, a
/// <see cref="Guid"/>), which the aggregate does not persist — it rides the domain event as a string.
/// An assignment made without a resolved external identity (e.g. test seeding via the two-argument
/// <c>AssignOperator</c>) carries a null or non-Guid value; those are skipped, since scoring could not
/// match them anyway.
/// </para>
/// </summary>
public sealed class PublishLiveSessionOperatorAssignedIntegrationEventHandler
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PublishLiveSessionOperatorAssignedIntegrationEventHandler> _logger;

    public PublishLiveSessionOperatorAssignedIntegrationEventHandler(
        IPublishEndpoint publishEndpoint,
        ILogger<PublishLiveSessionOperatorAssignedIntegrationEventHandler> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(LiveSessionOperatorAssignedEvent notification, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(notification.AssignedOperatorExternalId, out var assignedOperatorExternalId))
        {
            _logger.LogWarning(
                "Skipping LiveSessionOperatorAssignedIntegrationEvent for session {LiveSessionId}: "
                + "no resolvable operator external identity id on the assignment.",
                notification.LiveSessionId);
            return;
        }

        try
        {
            await _publishEndpoint.Publish(
                new LiveSessionOperatorAssignedIntegrationEvent(
                    notification.LiveSessionId,
                    assignedOperatorExternalId,
                    notification.OccurredAt),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to enqueue LiveSessionOperatorAssignedIntegrationEvent for session {LiveSessionId}.",
                notification.LiveSessionId);
            throw;
        }
    }
}
