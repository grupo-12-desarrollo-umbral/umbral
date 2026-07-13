using umbral_backend.Domain.Common;

namespace umbral_backend.Application.Common.Interfaces;

/// <summary>
/// Routes a raised domain event to its transactional-outbox integration publisher(s). Invoked by
/// <c>DispatchDomainEventsInterceptor</c> in <c>SavingChanges</c> — inside the SaveChanges that raised
/// the event — so each publisher's <c>IPublishEndpoint.Publish</c> (a local OutboxMessage insert under
/// the bus outbox) is flushed by the same transaction as the business write. Post-commit side effects
/// (SignalR broadcasts, orchestration) stay on the MediatR notification path in <c>SavedChanges</c>.
/// </summary>
public interface IOutboxDomainEventDispatcher
{
    Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken);
}
