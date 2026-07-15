using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// Proves both remaining MassTransit publish paths end-to-end (#165): each real MediatR handler
// publishes through IPublishEndpoint, RabbitMQ routes via the contract's short [EntityName]
// exchange, and a bound consumer receives the event. Docker unavailability skips gracefully.
public sealed class MassTransitRemainingIntegrationEventPublishTests
{
    [Fact]
    public async Task Handler_PublishesSessionResultsFinalized_BoundConsumerReceivesIt()
    {
        await WithRabbitMqAsync(async (busControl, probe) =>
        {
            var handler = new PublishSessionResultsFinalizedIntegrationEventHandler(
                busControl,
                NullLogger<PublishSessionResultsFinalizedIntegrationEventHandler>.Instance);
            var sessionId = Guid.NewGuid();
            var finishedAt = DateTimeOffset.UtcNow;

            await handler.Handle(
                new SessionStateChangedEvent(
                    sessionId,
                    SessionState.Active,
                    SessionState.Finished,
                    finishedAt),
                CancellationToken.None);

            var received = await probe.SessionResultsFinalized.Task.WaitAsync(TimeSpan.FromSeconds(15));
            received.Should().Be(new SessionResultsFinalizedIntegrationEvent(sessionId, finishedAt));
        });
    }

    [Fact]
    public async Task Handler_PublishesAnswerRegistered_BoundConsumerReceivesIt()
    {
        await WithRabbitMqAsync(async (busControl, probe) =>
        {
            var handler = new PublishAnswerRegisteredIntegrationEventHandler(
                busControl,
                NullLogger<PublishAnswerRegisteredIntegrationEventHandler>.Instance);
            var domainEvent = new AnswerRegisteredEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Gilded Owls",
                Guid.NewGuid(),
                Guid.NewGuid(),
                questionSequenceOrder: 3,
                selectedOptionSequenceOrder: 2,
                isCorrect: true,
                scoreValue: 150,
                DateTimeOffset.UtcNow);

            await handler.Handle(domainEvent, CancellationToken.None);

            var received = await probe.AnswerRegistered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            received.Should().Be(new AnswerRegisteredIntegrationEvent(
                domainEvent.LiveSessionId,
                domainEvent.TeamId,
                domainEvent.ReferenceTeamId,
                domainEvent.TeamDisplayName,
                domainEvent.EvidenceSubmissionId,
                domainEvent.ActiveSubstageId,
                domainEvent.QuestionSequenceOrder,
                domainEvent.SelectedOptionSequenceOrder,
                domainEvent.IsCorrect,
                domainEvent.ScoreValue,
                domainEvent.SubmittedAt));
        });
    }

    private static async Task WithRabbitMqAsync(Func<IBusControl, IntegrationEventProbe, Task> assertion)
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var probe = new IntegrationEventProbe();
            await using var provider = new ServiceCollection()
                .AddSingleton(probe)
                .AddMassTransit(bus =>
                {
                    bus.AddConsumer<SessionResultsFinalizedTestConsumer>();
                    bus.AddConsumer<AnswerRegisteredTestConsumer>();
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
                await assertion(busControl, probe);
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

    private sealed class IntegrationEventProbe
    {
        public TaskCompletionSource<SessionResultsFinalizedIntegrationEvent> SessionResultsFinalized { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<AnswerRegisteredIntegrationEvent> AnswerRegistered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SessionResultsFinalizedTestConsumer : IConsumer<SessionResultsFinalizedIntegrationEvent>
    {
        private readonly IntegrationEventProbe _probe;

        public SessionResultsFinalizedTestConsumer(IntegrationEventProbe probe) => _probe = probe;

        public Task Consume(ConsumeContext<SessionResultsFinalizedIntegrationEvent> context)
        {
            _probe.SessionResultsFinalized.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class AnswerRegisteredTestConsumer : IConsumer<AnswerRegisteredIntegrationEvent>
    {
        private readonly IntegrationEventProbe _probe;

        public AnswerRegisteredTestConsumer(IntegrationEventProbe probe) => _probe = probe;

        public Task Consume(ConsumeContext<AnswerRegisteredIntegrationEvent> context)
        {
            _probe.AnswerRegistered.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }
}
