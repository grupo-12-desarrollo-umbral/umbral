using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Messaging;

internal sealed class CrossServiceConsumerHarness : IAsyncDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan EffectTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan RepublishInterval = TimeSpan.FromSeconds(1);

    private readonly string _postgresConnectionString;
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private IHost? _consumerHost;
    private IBusControl? _publisherBus;

    public CrossServiceConsumerHarness(string postgresConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
    }

    public async Task StartAsync()
    {
        await DockerAvailability.StartOrSkipAsync(() => _rabbitMq.StartAsync(), _rabbitMq.DisposeAsync);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbral_backendDb"] = _postgresConnectionString,
            ["RabbitMq:HostName"] = _rabbitMq.Hostname,
            ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:UserName"] = "guest",
            ["RabbitMq:Password"] = "guest"
        });

        builder.AddApplicationServices();
        builder.AddInfrastructureServices();
        builder.Services.AddSingleton<ICurrentUser>(StubCurrentUser.Instance);
        builder.Services.AddSingleton<IRankingBroadcaster, NoOpRankingBroadcaster>();

        _consumerHost = builder.Build();
        using (var startCts = new CancellationTokenSource(StartTimeout))
        {
            await _consumerHost.StartAsync(startCts.Token).WaitAsync(StartTimeout, startCts.Token);
        }

        _publisherBus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(_rabbitMq.Hostname, _rabbitMq.GetMappedPublicPort(5672), "/", host =>
            {
                host.Username("guest");
                host.Password("guest");
            });
        });

        using var publisherStartCts = new CancellationTokenSource(StartTimeout);
        await _publisherBus.StartAsync(publisherStartCts.Token)
            .WaitAsync(StartTimeout, publisherStartCts.Token);
    }

    public async Task PublishAndWaitForEffectAsync<TMessage>(
        TMessage message,
        Func<ScoringMonitoringDbContext, CancellationToken, Task<bool>> effectObserved)
        where TMessage : class
    {
        var publisherBus = _publisherBus ?? throw new InvalidOperationException("Harness has not been started.");

        using var timeoutCts = new CancellationTokenSource(EffectTimeout);
        var nextPublishAt = DateTimeOffset.MinValue;

        while (!timeoutCts.IsCancellationRequested)
        {
            if (DateTimeOffset.UtcNow >= nextPublishAt)
            {
                await publisherBus.Publish(message, timeoutCts.Token);
                nextPublishAt = DateTimeOffset.UtcNow.Add(RepublishInterval);
            }

            await using var context = CreateDbContext();
            if (await effectObserved(context, timeoutCts.Token))
            {
                return;
            }

            await Task.Delay(PollInterval, timeoutCts.Token);
        }

        timeoutCts.Token.ThrowIfCancellationRequested();
    }

    public ScoringMonitoringDbContext CreateDbContext()
    {
        return new ScoringMonitoringDbContext(
            new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
                .UseNpgsql(_postgresConnectionString)
                .Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisherBus is not null)
        {
            await _publisherBus.StopAsync(CancellationToken.None).WaitAsync(StartTimeout);
        }

        if (_consumerHost is not null)
        {
            await _consumerHost.StopAsync(CancellationToken.None).WaitAsync(StartTimeout);
            _consumerHost.Dispose();
        }

        await _rabbitMq.DisposeAsync();
    }

    private sealed class NoOpRankingBroadcaster : IRankingBroadcaster
    {
        public Task RankingChanged(
            Guid liveSessionId,
            RankingSnapshotDto snapshot,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCurrentUser : ICurrentUser
    {
        public static readonly StubCurrentUser Instance = new();

        public string? Id => null;
        public string? Email => null;
        public string? Role => null;
    }
}
