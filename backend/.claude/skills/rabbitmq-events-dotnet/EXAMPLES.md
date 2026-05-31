# RabbitMQ Events .NET Examples

These examples are templates, not a framework. Adapt names, namespaces, and boundaries to the target project.

## Basic Registration

```csharp
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection("RabbitMq"))
    .ValidateDataAnnotations()
    .Validate(o => !string.IsNullOrWhiteSpace(o.HostName), "RabbitMq:HostName is required")
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

    return new ConnectionFactory
    {
        HostName = options.HostName,
        Port = options.Port,
        VirtualHost = options.VirtualHost,
        UserName = options.UserName,
        Password = options.Password,
        RequestedHeartbeat = TimeSpan.FromSeconds(options.RequestedHeartbeatSeconds),
        AutomaticRecoveryEnabled = options.AutomaticRecoveryEnabled,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(options.NetworkRecoveryIntervalSeconds),
        ClientProvidedName = options.ClientProvidedName
    };
});

builder.Services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddHostedService<OrderCreatedConsumerService>();

await builder.Build().RunAsync();
```

## Options Type

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class RabbitMqOptions
{
    [Required]
    public string HostName { get; init; } = null!;

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    [Required]
    public string VirtualHost { get; init; } = "/";

    [Required]
    public string UserName { get; init; } = null!;

    [Required]
    public string Password { get; init; } = null!;

    [Range(5, 300)]
    public int RequestedHeartbeatSeconds { get; init; } = 30;

    [Range(1, 300)]
    public int NetworkRecoveryIntervalSeconds { get; init; } = 10;

    public bool AutomaticRecoveryEnabled { get; init; } = true;

    [Required]
    public string ClientProvidedName { get; init; } = "app:unknown component:rabbitmq";
}
```

## Connection Provider

```csharp
using RabbitMQ.Client;

public interface IRabbitMqConnectionProvider
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken);
}

public sealed class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RabbitMqConnectionProvider(ConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            _connection = await _factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
```

## Publisher with Confirms and Mandatory Publish

```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IRabbitMqConnectionProvider _connections;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private IChannel? _channel;

    public RabbitMqEventPublisher(IRabbitMqConnectionProvider connections)
    {
        _connections = connections;
    }

    public async Task PublishAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString("n"),
            Type = typeof(T).Name,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: true, basicProperties: properties, body: body, cancellationToken: cancellationToken);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        var connection = await _connections.GetConnectionAsync(cancellationToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.ConfirmSelectAsync(cancellationToken);
        _channel.BasicReturnAsync += (_, args) =>
            Task.FromException(new InvalidOperationException(
                $"Unroutable publish: {args.Exchange}/{args.RoutingKey}"));

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _publishGate.Dispose();
    }
}
```

## Consumer Hosted Service

```csharp
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class OrderCreatedConsumerService : BackgroundService
{
    private readonly IRabbitMqConnectionProvider _connections;
    private IChannel? _channel;

    public OrderCreatedConsumerService(IRabbitMqConnectionProvider connections)
    {
        _connections = connections;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _connections.GetConnectionAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync("domain.events", ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await _channel.QueueDeclareAsync("orders.created.worker", durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync("orders.created.worker", "domain.events", "orders.created", cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 16, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var dto = JsonSerializer.Deserialize<OrderCreated>(
                    ea.Body.ToArray()) ?? throw new InvalidOperationException("Invalid payload");

                await HandleAsync(dto, stoppingToken);
                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (TransientDependencyException)
            {
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
            }
            catch
            {
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync("orders.created.worker", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private static Task HandleAsync(OrderCreated message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public sealed record OrderCreated(Guid OrderId, DateTimeOffset CreatedAtUtc);
```

## Retry Topology Sketch

Use this when broker policies are available:

1. Main queue consumes from `domain.events`.
2. Main queue has a policy-based DLX to `domain.events.retry`.
3. Retry queue applies TTL and dead-letters back to the main exchange and routing key.
4. Poison messages eventually route to a terminal DLQ.

Prefer policy-managed TTL and DLX over hardcoded queue arguments when operations need to tune behavior without redeploying the app.

## Operational Checklist

- Confirm the app uses long-lived connections.
- Confirm publishing channels are not used concurrently without serialization.
- Confirm consumers use manual ack.
- Confirm prefetch is bounded.
- Confirm `mandatory` publishes are surfaced.
- Confirm retries do not create infinite hot loops.
- Confirm message IDs or equivalent dedupe keys exist.
