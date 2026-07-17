using umbral_backend.Domain.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface IOutboxDomainEventDispatcher
{
    Task DispatchAsync(BaseEvent domainEvent, CancellationToken cancellationToken);
}
