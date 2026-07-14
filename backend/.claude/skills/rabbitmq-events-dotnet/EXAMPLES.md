# MassTransit + RabbitMQ Examples

## Integration-event contract

```csharp
using MassTransit;

[EntityName("orders-order-created")]
public sealed record OrderCreatedIntegrationEvent(Guid OrderId, DateTimeOffset CreatedAt);
```

## Publish from a MediatR notification handler

```csharp
using MassTransit;
using MediatR;

public sealed class PublishOrderCreatedIntegrationEventHandler(
    IPublishEndpoint publishEndpoint,
    ILogger<PublishOrderCreatedIntegrationEventHandler> logger)
    : INotificationHandler<OrderCreatedEvent>
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);

    public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        var message = new OrderCreatedIntegrationEvent(notification.OrderId, notification.CreatedAt);

        try
        {
            using var publishTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            publishTimeout.CancelAfter(PublishTimeout);
            await publishEndpoint.Publish(message, publishTimeout.Token);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to publish order-created event for {OrderId}", notification.OrderId);
        }
    }
}
```

Post-commit publishing is bounded-blocking and best-effort: always link the timeout to the caller,
cap it at five seconds, and log and swallow timeout or publication failures.

## Infrastructure registration

```csharp
builder.Services.AddMassTransit(bus =>
{
    bus.AddConsumer<OrderCreatedConsumer>();

    bus.UsingRabbitMq((context, cfg) =>
    {
        var options = context.GetRequiredService<IOptions<MassTransitRabbitMqOptions>>().Value;
        cfg.Host(options.HostName, (ushort)options.Port, options.VirtualHost, host =>
        {
            host.Username(options.UserName);
            host.Password(options.Password);
        });
        cfg.ConfigureEndpoints(context);
    });
});
```

## Consumer

```csharp
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        return HandleIdempotently(context.Message, context.CancellationToken);
    }
}
```

Unhandled consumer faults are moved by MassTransit to the receive endpoint's `_error` queue.

## Verification checklist

- Application references `MassTransit.Abstractions`, not `MassTransit.RabbitMQ` or `RabbitMQ.Client`.
- Infrastructure contains the only `UsingRabbitMq` and broker-credential configuration.
- Contracts use stable `[EntityName]` exchange names.
- Handlers publish through `IPublishEndpoint` and have mapping/failure-policy unit tests.
- Post-commit publishes use a caller-linked five-second timeout and cannot fault committed work.
- A Testcontainers integration test proves a real `IConsumer<T>` receives each event.
