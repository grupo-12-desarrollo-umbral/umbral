using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Messaging;

[Collection(PostgreSqlCollection.Name)]
public sealed class MassTransitScoreEntryFlowTests
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

    private readonly PostgreSqlFixture _fixture;

    public MassTransitScoreEntryFlowTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishingAnswerRegistered_PersistsScoreEntry_AndPublishesScoreEntryRegistered()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var probe = new ScoreEntryRegisteredProbe();
            using var cts = new CancellationTokenSource(StartTimeout);

            await using var probeProvider = new ServiceCollection()
                .AddSingleton(probe)
                .AddMassTransit(bus =>
                {
                    bus.AddConsumer<ScoreEntryRegisteredProbeConsumer>();
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

            var builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbral_backendDb"] = _fixture.ConnectionString,
                ["RabbitMq:HostName"] = rabbit.Hostname,
                ["RabbitMq:Port"] = rabbit.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:VirtualHost"] = "/",
                ["RabbitMq:UserName"] = "guest",
                ["RabbitMq:Password"] = "guest"
            });

            builder.Services.AddLogging(logging => logging.AddDebug().SetMinimumLevel(LogLevel.Debug));
            builder.AddApplicationServices();
            builder.AddInfrastructureServices();
            builder.Services.AddSingleton<IRankingBroadcaster, NoOpRankingBroadcaster>();

            using var host = builder.Build();

            var probeBus = probeProvider.GetRequiredService<IBusControl>();
            await probeBus.StartAsync(cts.Token).WaitAsync(StartTimeout, cts.Token);

            try
            {
                await host.StartAsync(cts.Token).WaitAsync(StartTimeout, cts.Token);

                var liveSessionId = Guid.NewGuid();
                var teamId = Guid.NewGuid();
                var submissionId = Guid.NewGuid();

                await probeBus.Publish(
                    new AnswerRegisteredIntegrationEvent(
                        liveSessionId,
                        teamId,
                        submissionId,
                        Guid.NewGuid(),
                        1,
                        2,
                        true,
                        250,
                        DateTimeOffset.UtcNow),
                    cts.Token);

                var published = await probe.Received.Task.WaitAsync(ReceiveTimeout, cts.Token);

                published.LiveSessionId.Should().Be(liveSessionId);
                published.TeamId.Should().Be(teamId);
                published.SourceEntityId.Should().Be(submissionId);

                await using var verificationContext = new ScoringMonitoringDbContext(
                    new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
                        .UseNpgsql(_fixture.ConnectionString)
                        .Options);

                var persisted = await verificationContext.ScoreEntries
                    .SingleAsync(scoreEntry => scoreEntry.SourceEntityId == submissionId, cts.Token);

                persisted.LiveSessionId.Should().Be(liveSessionId);
                persisted.TeamId.Should().Be(teamId);
                persisted.ScoreValue.Value.Should().Be(250);
            }
            finally
            {
                await host.StopAsync(CancellationToken.None).WaitAsync(StartTimeout);
                await probeBus.StopAsync(CancellationToken.None).WaitAsync(StartTimeout);
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    private sealed class NoOpRankingBroadcaster : IRankingBroadcaster
    {
        public Task RankingChanged(Guid liveSessionId, RankingSnapshotDto snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ScoreEntryRegisteredProbe
    {
        public TaskCompletionSource<ScoreEntryRegisteredIntegrationEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ScoreEntryRegisteredProbeConsumer : IConsumer<ScoreEntryRegisteredIntegrationEvent>
    {
        private readonly ScoreEntryRegisteredProbe _probe;

        public ScoreEntryRegisteredProbeConsumer(ScoreEntryRegisteredProbe probe)
        {
            _probe = probe;
        }

        public Task Consume(ConsumeContext<ScoreEntryRegisteredIntegrationEvent> context)
        {
            _probe.Received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }
}
