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
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

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
            builder.Services.AddSingleton<ICurrentUser>(new StubCurrentUser());
            builder.Services.AddSingleton<IRankingBroadcaster, NoOpRankingBroadcaster>();

            using var host = builder.Build();

            var probeBus = probeProvider.GetRequiredService<IBusControl>();
            using var probeStartCts = new CancellationTokenSource(StartTimeout);
            await probeBus.StartAsync(probeStartCts.Token).WaitAsync(StartTimeout, probeStartCts.Token);

            try
            {
                using var hostStartCts = new CancellationTokenSource(StartTimeout);
                await host.StartAsync(hostStartCts.Token).WaitAsync(StartTimeout, hostStartCts.Token);

                var liveSessionId = Guid.NewGuid();
                var teamId = Guid.NewGuid();
                var referenceTeamId = Guid.NewGuid();
                var submissionId = Guid.NewGuid();
                var answerRegistered = new AnswerRegisteredIntegrationEvent(
                    liveSessionId,
                    teamId,
                    referenceTeamId,
                    "Gilded Owls",
                    submissionId,
                    Guid.NewGuid(),
                    1,
                    2,
                    true,
                    250,
                    DateTimeOffset.UtcNow);

                await PublishUntilPersistedAsync(probeBus, answerRegistered, submissionId);

                var published = await probe.Received.Task.WaitAsync(ReceiveTimeout);

                published.LiveSessionId.Should().Be(liveSessionId);
                published.TeamId.Should().Be(referenceTeamId);
                published.SourceEntityId.Should().Be(submissionId);

                await using var verificationContext = new ScoringMonitoringDbContext(
                    new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
                        .UseNpgsql(_fixture.ConnectionString)
                        .Options);

                var persisted = await verificationContext.ScoreEntries
                    .SingleAsync(scoreEntry => scoreEntry.SourceEntityId == submissionId);

                persisted.LiveSessionId.Should().Be(liveSessionId);
                persisted.TeamId.Should().Be(referenceTeamId);
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

    private sealed class StubCurrentUser : ICurrentUser
    {
        public string? Id => "integration-test";
        public string? Email => null;
        public string? Role => null;
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

    private async Task PublishUntilPersistedAsync(
        IBusControl probeBus,
        AnswerRegisteredIntegrationEvent answerRegistered,
        Guid submissionId)
    {
        using var publishBudget = new CancellationTokenSource(ReceiveTimeout);

        while (!publishBudget.IsCancellationRequested)
        {
            await probeBus.Publish(answerRegistered, publishBudget.Token);

            if (await ScoreEntryExistsAsync(submissionId, publishBudget.Token))
            {
                return;
            }

            await Task.Delay(RetryDelay, publishBudget.Token);
        }

        publishBudget.Token.ThrowIfCancellationRequested();
    }

    private async Task<bool> ScoreEntryExistsAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        await using var verificationContext = new ScoringMonitoringDbContext(
            new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
                .UseNpgsql(_fixture.ConnectionString)
                .Options);

        return await verificationContext.ScoreEntries
            .AnyAsync(scoreEntry => scoreEntry.SourceEntityId == submissionId, cancellationToken);
    }
}
