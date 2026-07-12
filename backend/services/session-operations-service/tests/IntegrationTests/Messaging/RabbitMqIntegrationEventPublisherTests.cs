using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Infrastructure.Messaging;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// Verifies the RabbitMQ transport (HU-33B, X.3): a published integration event lands on a
// bound queue of the durable topic exchange, and a broker-down publish logs + does not throw (D-3).
public sealed class RabbitMqIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishedEvent_IsReceivedOnBoundQueue()
    {
        // Pin the container to the publisher's production defaults (guest/guest); the module
        // otherwise defaults to rabbitmq/rabbitmq → ACCESS_REFUSED against guest/guest options.
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var options = new RabbitMqOptions
            {
                HostName = rabbit.Hostname,
                Port = rabbit.GetMappedPublicPort(5672),
                UserName = "guest",
                Password = "guest",
            };

            const string queueName = "test.session.question.closed";
            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(
                queueName, options.Exchange, RabbitMqIntegrationEventPublisher.QuestionClosedRoutingKey);

            var sessionId = Guid.NewGuid();
            await using (var publisher = new RabbitMqIntegrationEventPublisher(
                Options.Create(options), NullLogger<RabbitMqIntegrationEventPublisher>.Instance))
            {
                await publisher.PublishAsync(
                    new QuestionClosedIntegrationEvent(sessionId, 2, DateTimeOffset.UtcNow), CancellationToken.None);

                BasicGetResult? delivery = null;
                for (var attempt = 0; attempt < 50 && delivery is null; attempt++)
                {
                    delivery = await channel.BasicGetAsync(queueName, autoAck: true);
                    if (delivery is null)
                    {
                        await Task.Delay(200);
                    }
                }

                delivery.Should().NotBeNull("the published event must land on the bound queue");
                var received = JsonSerializer.Deserialize<QuestionClosedIntegrationEvent>(delivery!.Body.Span);
                received!.LiveSessionId.Should().Be(sessionId);
                received.QuestionIndex.Should().Be(2);
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    // HU-34: the accepted-answer contract publishes through the SAME service-owned exchange on its
    // own routing key, proving the existing transport carries the new contract without new infra.
    [Fact]
    public async Task PublishedAnswerRegisteredEvent_IsReceivedOnBoundQueue()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var options = new RabbitMqOptions
            {
                HostName = rabbit.Hostname,
                Port = rabbit.GetMappedPublicPort(5672),
                UserName = "guest",
                Password = "guest",
            };

            const string queueName = "test.session.answer.registered";
            var factory = new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password,
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync(options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
            await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(
                queueName, options.Exchange, RabbitMqIntegrationEventPublisher.AnswerRegisteredRoutingKey);

            var sessionId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();
            var substageSnapshotId = Guid.NewGuid();
            var submittedAt = DateTimeOffset.UtcNow;

            await using (var publisher = new RabbitMqIntegrationEventPublisher(
                Options.Create(options), NullLogger<RabbitMqIntegrationEventPublisher>.Instance))
            {
                await publisher.PublishAsync(
                    new AnswerRegisteredIntegrationEvent(
                        sessionId, teamId, submissionId, substageSnapshotId, 1, 1, true, 50, submittedAt),
                    CancellationToken.None);

                BasicGetResult? delivery = null;
                for (var attempt = 0; attempt < 50 && delivery is null; attempt++)
                {
                    delivery = await channel.BasicGetAsync(queueName, autoAck: true);
                    if (delivery is null)
                    {
                        await Task.Delay(200);
                    }
                }

                delivery.Should().NotBeNull("the published answer event must land on the bound queue");
                var received = JsonSerializer.Deserialize<AnswerRegisteredIntegrationEvent>(delivery!.Body.Span);
                received!.LiveSessionId.Should().Be(sessionId);
                received.TeamId.Should().Be(teamId);
                received.TriviaAnswerSubmissionId.Should().Be(submissionId);
                received.IsCorrect.Should().BeTrue();
                received.ScoreValue.Should().Be(50);
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    [Fact]
    public async Task BrokerDownPublish_LogsAndDoesNotThrow()
    {
        var logger = new CapturingLogger<RabbitMqIntegrationEventPublisher>();
        var options = Options.Create(new RabbitMqOptions { HostName = "127.0.0.1", Port = 1 });

        await using (var publisher = new RabbitMqIntegrationEventPublisher(options, logger))
        {
            var publish = () => publisher.PublishAsync(
                new SessionResultsFinalizedIntegrationEvent(Guid.NewGuid(), DateTimeOffset.UtcNow),
                CancellationToken.None);

            await publish.Should().NotThrowAsync("a broker failure must never propagate into the dispatch (D-3)");
        }

        // DisposeAsync drains the background loop; the failed publish must have been logged, not thrown.
        logger.Errors.Should().NotBeEmpty("a broker-down publish must be logged");
    }

    [Fact]
    public async Task PublishAsync_WhenEventHasNoRoutingKey_LogsWarningAndDoesNotDrainToBroker()
    {
        var logger = new CapturingLogger<RabbitMqIntegrationEventPublisher>();
        var options = Options.Create(new RabbitMqOptions { HostName = "127.0.0.1", Port = 1 });

        await using (var publisher = new RabbitMqIntegrationEventPublisher(options, logger))
        {
            var publish = () => publisher.PublishAsync(new UnmappedIntegrationEvent(Guid.NewGuid()), CancellationToken.None);

            await publish.Should().NotThrowAsync();
        }

        logger.Warnings.Should().ContainSingle(message => message.Contains(nameof(UnmappedIntegrationEvent), StringComparison.Ordinal));
        logger.Errors.Should().BeEmpty("unmapped events return before the background drain touches RabbitMQ");
    }

    private sealed record UnmappedIntegrationEvent(Guid Id);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Warnings { get; } = new();

        public List<string> Errors { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
            else if (logLevel >= LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
