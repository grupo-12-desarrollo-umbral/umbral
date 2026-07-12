using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common;
using Xunit;

namespace umbral_backend.Infrastructure.IntegrationTests.Messaging;

public sealed class AnswerRegisteredConsumerTests
{
    [Fact]
    public async Task PublishedAnswerRegisteredEvent_IsReceivedByScoringMonitoringConsumer()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var probe = new ReceiptProbe();
            var settings = new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = rabbit.Hostname,
                ["RabbitMq:Port"] = rabbit.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:VirtualHost"] = "/",
                ["RabbitMq:UserName"] = "guest",
                ["RabbitMq:Password"] = "guest"
            };
            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(settings);
            builder.AddApplicationServices();
            builder.AddInfrastructureServices();
            builder.Services.AddSingleton<IAnswerReceiptTestSeam>(probe);

            using var host = builder.Build();
            await host.StartAsync();

            var expected = new AnswerRegisteredIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                3, 2, true, 150, DateTimeOffset.UtcNow);
            await host.Services.GetRequiredService<IPublishEndpoint>().Publish(expected);

            var received = await probe.Received.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(expected, received);

            await host.StopAsync();
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    private sealed class ReceiptProbe : IAnswerReceiptTestSeam
    {
        public TaskCompletionSource<AnswerRegisteredIntegrationEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReceivedAsync(
            AnswerRegisteredIntegrationEvent message,
            CancellationToken cancellationToken)
        {
            Received.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
