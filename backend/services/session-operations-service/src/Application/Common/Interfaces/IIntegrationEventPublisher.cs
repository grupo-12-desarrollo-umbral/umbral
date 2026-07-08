namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Outbound seam for publishing integration events after transactional success (HU-33B).
/// The RabbitMQ implementation lives in Infrastructure (X.3); the runtime never depends on
/// the broker — publish is best-effort and failures are swallowed at the bridge (D-3, AC #6).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TIntegrationEvent : notnull;
}
