using MassTransit;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Commands.RecordAnswerReceipt;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Infrastructure.Messaging;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

// Proves the broker → consumer → handler path end-to-end (#191): the REAL service registrations
// (AddApplicationServices + AddMassTransitMessaging, binding AnswerRegisteredConsumer to the
// [EntityName("session-answer-registered")] exchange) run on a real RabbitMQ broker; a separate bus
// publishes the contract; the TCS-completing IAnswerReceiptProbe fake fires, proving the whole
// transport adapter → MediatR command → thin handler chain executed. Skips gracefully when Docker
// is unavailable, matching the other integration tests.
public sealed class AnswerRegisteredConsumeTests
{
    private sealed class ProbeCompletionSource : IAnswerReceiptProbe
    {
        public TaskCompletionSource<RecordAnswerReceiptCommand> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Record(RecordAnswerReceiptCommand receipt, CancellationToken cancellationToken)
        {
            Received.TrySetResult(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public string? Id => "scoring-consumer";
        public string? Email => null;
        public string? Role => null;
    }

    [Fact]
    public async Task PublishedAnswerRegistered_IsConsumedAndReachesTheProbe()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var probe = new ProbeCompletionSource();

            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<IAnswerReceiptProbe>(probe);
            builder.Services.AddScoped<ICurrentUser, StubCurrentUser>();
            builder.Services.Configure<RabbitMqOptions>(options =>
            {
                options.HostName = rabbit.Hostname;
                options.Port = rabbit.GetMappedPublicPort(5672);
                options.VirtualHost = "/";
                options.UserName = "guest";
                options.Password = "guest";
            });
            builder.AddApplicationServices();
            builder.AddMassTransitMessaging();

            using var host = builder.Build();
            await host.StartAsync();

            try
            {
                await using var publisherProvider = new ServiceCollection()
                    .AddMassTransit(bus => bus.UsingRabbitMq((_, cfg) =>
                        cfg.Host(rabbit.Hostname, rabbit.GetMappedPublicPort(5672), "/", h =>
                        {
                            h.Username("guest");
                            h.Password("guest");
                        })))
                    .BuildServiceProvider();

                var publishBus = publisherProvider.GetRequiredService<IBusControl>();
                await publishBus.StartAsync();
                try
                {
                    var sessionId = Guid.NewGuid();
                    var teamId = Guid.NewGuid();

                    await publishBus.Publish(new AnswerRegisteredIntegrationEvent(
                        sessionId, teamId, Guid.NewGuid(), Guid.NewGuid(),
                        QuestionSequenceOrder: 4, SelectedOptionSequenceOrder: 2,
                        IsCorrect: true, ScoreValue: 25, SubmittedAt: DateTimeOffset.UtcNow));

                    var received = await probe.Received.Task.WaitAsync(TimeSpan.FromSeconds(20));

                    received.LiveSessionId.Should().Be(sessionId);
                    received.TeamId.Should().Be(teamId);
                }
                finally
                {
                    await publishBus.StopAsync();
                }
            }
            finally
            {
                await host.StopAsync();
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }
}
