using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ transport for outbound integration events (HU-33B, X.3). Publishes to a durable
/// <c>topic</c> exchange with persistent, publisher-confirmed messages over a long-lived
/// connection/channel. Best-effort (D-3): publishes are handed to a background channel so a
/// slow or unreachable broker never adds latency to — or throws into — the SaveChanges
/// dispatch. Broker failures are logged and swallowed; the message is dropped (durable replay
/// is the downstream consumer's concern, HU-37A). Each broker round-trip is bounded by
/// <see cref="RabbitMqOptions.PublishConfirmTimeout"/> so a hung broker cannot stall the drain
/// task, and dispose cancels the drain so shutdown never blocks on the broker.
/// </summary>
public sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher, IAsyncDisposable
{
    public const string QuestionClosedRoutingKey = "session.question.closed";
    public const string SessionResultsFinalizedRoutingKey = "session.results.finalized";
    public const string AnswerRegisteredRoutingKey = "session.answer.registered";

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqIntegrationEventPublisher> _logger;
    private readonly Channel<OutboundMessage> _outbox;
    private readonly Task _drainTask;

    // Dispose backstop: cancelled if the drain overruns, unblocking a drain parked on a hung
    // broker (mid-confirm/connect) or waiting for messages so shutdown never hangs.
    private readonly CancellationTokenSource _shutdown = new();

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqIntegrationEventPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqIntegrationEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        // ponytail: bounded drop-oldest outbox — best-effort transport (D-3), not a durable
        // outbox (that reliability belongs to the HU-37A consumer, not this producer).
        _outbox = Channel.CreateBounded<OutboundMessage>(
            new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });

        _drainTask = Task.Run(DrainAsync);
    }

    public Task PublishAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        where TIntegrationEvent : notnull
    {
        var routingKey = ResolveRoutingKey(integrationEvent);
        if (routingKey is null)
        {
            _logger.LogWarning(
                "No routing key mapped for integration event {EventType}; skipping publish.",
                integrationEvent.GetType().Name);
            return Task.CompletedTask;
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(integrationEvent, integrationEvent.GetType());
        var message = new OutboundMessage(routingKey, integrationEvent.GetType().Name, body);

        // Non-blocking hand-off: the runtime thread never waits on the broker (D-3, AC #6).
        if (!_outbox.Writer.TryWrite(message))
        {
            _logger.LogWarning("Integration-event outbox full; dropped {EventType}.", message.TypeName);
        }

        return Task.CompletedTask;
    }

    private static string? ResolveRoutingKey(object integrationEvent) => integrationEvent switch
    {
        QuestionClosedIntegrationEvent => QuestionClosedRoutingKey,
        SessionResultsFinalizedIntegrationEvent => SessionResultsFinalizedRoutingKey,
        AnswerRegisteredIntegrationEvent => AnswerRegisteredRoutingKey,
        _ => null,
    };

    private async Task DrainAsync()
    {
        try
        {
            // ReadAllAsync observes shutdown so a drain parked waiting for messages exits promptly.
            await foreach (var message in _outbox.Reader.ReadAllAsync(_shutdown.Token))
            {
                // Bound every broker round-trip: a broker reachable over TCP but never acking a
                // publisher-confirm must not block this single drain task forever. Linked so the
                // await also unblocks on dispose (_shutdown), not just on the confirm timeout.
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                linked.CancelAfter(_options.PublishConfirmTimeout);

                try
                {
                    var channel = await EnsureChannelAsync(linked.Token);

                    var properties = new BasicProperties
                    {
                        ContentType = "application/json",
                        Persistent = true,
                        MessageId = Guid.NewGuid().ToString("n"),
                        Type = message.TypeName,
                    };

                    // Confirm-tracking channel: this await completes on broker ack — off the
                    // runtime thread, so a slow broker adds latency here only, never to dispatch.
                    await channel.BasicPublishAsync(
                        exchange: _options.Exchange,
                        routingKey: message.RoutingKey,
                        mandatory: false,
                        basicProperties: properties,
                        body: message.Body,
                        cancellationToken: linked.Token);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    // Graceful shutdown: stop draining so DisposeAsync completes promptly.
                    break;
                }
                catch (Exception exception)
                {
                    // Broker down, hung (confirm timeout), or NACK: log + reset + drop (D-3).
                    _logger.LogError(
                        exception,
                        "Failed to publish integration event {EventType} to RabbitMQ; dropping (best-effort, D-3).",
                        message.TypeName);
                    await ResetChannelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown cancelled the read wait; clean exit.
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_connection is not { IsOpen: true })
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                ClientProvidedName = "session-operations:integration-event-publisher",
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
        }

        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await _channel.ExchangeDeclareAsync(
            _options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }
        }
        catch
        {
            // best-effort teardown
        }
        finally
        {
            _channel = null;
        }

        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }
        catch
        {
            // best-effort teardown
        }
        finally
        {
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbox.Writer.TryComplete();

        try
        {
            // Let the drain flush already-queued messages (each bounded by PublishConfirmTimeout,
            // so this can't hang) and log any broker failures — preserving best-effort D-3 logging.
            // Cancel as a backstop only if it overruns: a broker hung mid-confirm must never block
            // shutdown until a force-kill.
            await _drainTask.WaitAsync(_options.PublishConfirmTimeout);
        }
        catch (TimeoutException)
        {
            _shutdown.Cancel();
            try
            {
                await _drainTask;
            }
            catch
            {
                // drain loop is best-effort; nothing to surface on shutdown
            }
        }
        catch
        {
            // drain loop is best-effort; nothing to surface on shutdown
        }

        await ResetChannelAsync();
        _shutdown.Dispose();
    }

    private readonly record struct OutboundMessage(string RoutingKey, string TypeName, byte[] Body);
}
