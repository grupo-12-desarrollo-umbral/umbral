using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// Proves the MassTransit publish path end-to-end (#164): the real handler publishes a
// QuestionClosedIntegrationEvent through IPublishEndpoint, MassTransit routes it over the
// [EntityName("session-question-closed")] exchange on a real RabbitMQ broker, and a bound consumer
// receives it. Skips gracefully when Docker/Testcontainers is unavailable, matching the existing tests.
public sealed class MassTransitQuestionClosedPublishTests
{
    [Fact]
    public async Task Handler_PublishesQuestionClosed_BoundConsumerReceivesIt()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        try
        {
            await rabbit.StartAsync();
        }
        catch (Exception)
        {
            // Docker/Testcontainers unavailable — nothing to assert without a broker.
            await rabbit.DisposeAsync();
            return;
        }

        try
        {
            var probe = new QuestionClosedProbe();
            await using var provider = new ServiceCollection()
                .AddSingleton(probe)
                .AddMassTransit(bus =>
                {
                    bus.AddConsumer<QuestionClosedTestConsumer>();
                    bus.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host(rabbit.Hostname, rabbit.GetMappedPublicPort(5672), "/", host =>
                        {
                            host.Username("guest");
                            host.Password("guest");
                        });
                        cfg.ConfigureEndpoints(context);
                    });
                })
                .BuildServiceProvider();

            var busControl = provider.GetRequiredService<IBusControl>();
            await busControl.StartAsync();
            try
            {
                // IBusControl is an IPublishEndpoint; feed it to the real handler exactly as DI would.
                var handler = new PublishQuestionClosedIntegrationEventHandler(
                    busControl, NullLogger<PublishQuestionClosedIntegrationEventHandler>.Instance);

                var sessionId = Guid.NewGuid();
                var closedAt = DateTimeOffset.UtcNow;
                await handler.Handle(
                    new QuestionClosedEvent(sessionId, questionIndex: 4, closedAt, wasExpiredByTimer: true),
                    CancellationToken.None);

                var received = await probe.Received.Task.WaitAsync(TimeSpan.FromSeconds(15));

                received.LiveSessionId.Should().Be(sessionId);
                received.QuestionIndex.Should().Be(4);
            }
            finally
            {
                await busControl.StopAsync();
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    private sealed class QuestionClosedProbe
    {
        public TaskCompletionSource<QuestionClosedIntegrationEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class QuestionClosedTestConsumer : IConsumer<QuestionClosedIntegrationEvent>
    {
        private readonly QuestionClosedProbe _probe;

        public QuestionClosedTestConsumer(QuestionClosedProbe probe) => _probe = probe;

        public Task Consume(ConsumeContext<QuestionClosedIntegrationEvent> context)
        {
            _probe.Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }
}
