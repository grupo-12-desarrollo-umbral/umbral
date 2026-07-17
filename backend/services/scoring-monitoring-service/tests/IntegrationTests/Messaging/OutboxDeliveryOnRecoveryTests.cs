using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Rankings;
using umbral_backend.Application.Rankings.Common;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Messaging;

[Collection(PostgreSqlCollection.Name)]
public sealed class OutboxDeliveryOnRecoveryTests
{
    private readonly string _connectionString;

    public OutboxDeliveryOnRecoveryTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task PendingPenaltyEvent_DrainsOnRecovery_AndIncrementsRankingVersion()
    {
        var rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();
        await DockerAvailability.StartOrSkipAsync(() => rabbit.StartAsync(), rabbit.DisposeAsync);

        try
        {
            var seeded = await SeedRankingAsync();
            var penaltyEntry = await CapturePenaltyAndPendingOutboxRowAsync(rabbit, seeded);

            await using (var verifyContext = CreateContext())
            {
                (await verifyContext.ScoreEntries.AnyAsync(entry => entry.ScoreEntryId == penaltyEntry.ScoreEntryId))
                    .Should().BeTrue("the ledger write must commit while delivery is unavailable");
                (await verifyContext.Set<OutboxMessage>()
                    .CountAsync(message => message.MessageType.Contains(nameof(ScoreEntryRegisteredIntegrationEvent))))
                    .Should().Be(1, "the score event must commit atomically as a pending outbox message");
            }

            using var recoveryHost = BuildHost(rabbit);
            await recoveryHost.StartAsync();
            try
            {
                await AssertRankingRecalculatedAsync(seeded.LiveSessionId);
                await AssertOutboxDrainedAsync();
            }
            finally
            {
                await recoveryHost.StopAsync();
            }
        }
        finally
        {
            await rabbit.DisposeAsync();
        }
    }

    private async Task<SeededRanking> SeedRankingAsync()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var grant = ScoreEntry.Grant(
            liveSessionId,
            teamId,
            "Gilded Owls",
            "trivia-answer-correct",
            ScoreValue.Create(150),
            recordedAt,
            ScoreSourceType.TriviaAnswerSubmission,
            Guid.NewGuid());
        var ranking = Ranking.Create(
            liveSessionId,
            [grant],
            recordedAt.AddSeconds(1),
            calculationVersion: 1,
            new ResolutionTimeRankingPolicy(),
            new Dictionary<Guid, string> { [teamId] = "Gilded Owls" });

        await using var context = CreateContext();
        context.ScoreEntries.Add(grant);
        context.Rankings.Add(ranking);
        await context.SaveChangesAsync();

        return new SeededRanking(liveSessionId, teamId, recordedAt);
    }

    private async Task<ScoreEntry> CapturePenaltyAndPendingOutboxRowAsync(
        RabbitMqContainer rabbit,
        SeededRanking seeded)
    {
        using var host = BuildHost(rabbit);
        var bus = host.Services.GetRequiredService<IBusControl>();
        await bus.StartAsync();
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ScoringMonitoringDbContext>();
            var scoreEntryId = Guid.NewGuid();
            var penalty = Penalty.Create(scoreEntryId, "Manual operator penalty", Guid.NewGuid());
            var penaltyEntry = ScoreEntry.Penalty(
                scoreEntryId,
                seeded.LiveSessionId,
                seeded.TeamId,
                string.Empty,
                "Manual operator penalty",
                ScoreValue.Create(100),
                penalty.PenaltyId,
                seeded.RecordedAt.AddMinutes(1));

            context.ScoreEntries.Add(penaltyEntry);
            context.Penalties.Add(penalty);
            await context.SaveChangesAsync();

            return penaltyEntry;
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private IHost BuildHost(RabbitMqContainer rabbit)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbral_backendDb"] = _connectionString,
            ["RabbitMq:HostName"] = rabbit.Hostname,
            ["RabbitMq:Port"] = rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:UserName"] = "guest",
            ["RabbitMq:Password"] = "guest"
        });
        builder.AddApplicationServices();
        builder.AddInfrastructureServices();
        builder.Services.AddSingleton<ICurrentUser>(StubCurrentUser.Instance);
        builder.Services.AddSingleton<IRankingBroadcaster, NoOpRankingBroadcaster>();
        return builder.Build();
    }

    private ScoringMonitoringDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        return new ScoringMonitoringDbContext(options);
    }

    private async Task AssertRankingRecalculatedAsync(Guid liveSessionId)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await using var context = CreateContext();
            var ranking = await context.Rankings
                .AsNoTracking()
                .SingleAsync(candidate => candidate.LiveSessionId == liveSessionId);
            if (ranking.CalculationVersion == 2)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        await using var finalContext = CreateContext();
        var finalVersion = await finalContext.Rankings
            .Where(ranking => ranking.LiveSessionId == liveSessionId)
            .Select(ranking => ranking.CalculationVersion)
            .SingleAsync();
        finalVersion.Should().Be(2, "outbox recovery must complete the score-to-ranking chain");
    }

    private async Task AssertOutboxDrainedAsync()
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await using var context = CreateContext();
            var remaining = await context.Set<OutboxMessage>()
                .CountAsync(message => message.MessageType.Contains(nameof(ScoreEntryRegisteredIntegrationEvent)));
            if (remaining == 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        await using var finalContext = CreateContext();
        (await finalContext.Set<OutboxMessage>()
            .CountAsync(message => message.MessageType.Contains(nameof(ScoreEntryRegisteredIntegrationEvent))))
            .Should().Be(0, "the delivery service removes a score event after broker delivery");
    }

    private sealed record SeededRanking(Guid LiveSessionId, Guid TeamId, DateTimeOffset RecordedAt);

    private sealed class StubCurrentUser : ICurrentUser
    {
        public static readonly StubCurrentUser Instance = new();
        public string? Id => "integration-test";
        public string? Email => null;
        public string? Role => null;
    }

    private sealed class NoOpRankingBroadcaster : IRankingBroadcaster
    {
        public Task RankingChanged(
            Guid liveSessionId,
            RankingSnapshotDto snapshot,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
