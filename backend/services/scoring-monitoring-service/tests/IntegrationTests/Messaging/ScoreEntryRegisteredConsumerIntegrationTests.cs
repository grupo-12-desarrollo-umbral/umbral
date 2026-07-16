using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Consumers;
using umbral_backend.Application.Scores.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence.Repositories;
using umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Messaging;

[Collection(PostgreSqlCollection.Name)]
public sealed class ScoreEntryRegisteredConsumerIntegrationTests
{
    private readonly string _connectionString;
    private readonly PersistenceTestContextFactory _contextFactory;

    public ScoreEntryRegisteredConsumerIntegrationTests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task Consume_WhenPenaltyDropsTotalBelowZero_RecalculatesRankingToClampedZero_AndPreservesTeamName()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var sessionStart = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var grant = ScoreEntry.Grant(
            liveSessionId,
            teamId,
            "Gilded Owls",
            "trivia-answer-correct",
            ScoreValue.Create(50),
            sessionStart,
            ScoreSourceType.TriviaAnswerSubmission,
            Guid.NewGuid());

        await using (var seedContext = _contextFactory.Create())
        {
            var scoreEntryRepository = new ScoreEntryRepository(seedContext);
            var rankingRepository = new RankingRepository(seedContext);

            await scoreEntryRepository.AddAsync(grant, CancellationToken.None);

            var initialRanking = Ranking.Create(
                liveSessionId,
                new[] { grant },
                sessionStart.AddSeconds(1),
                calculationVersion: 1,
                new ResolutionTimeRankingPolicy(),
                new Dictionary<Guid, string> { [teamId] = "Gilded Owls" });

            await rankingRepository.SaveAsync(initialRanking, CancellationToken.None);
        }

        var scoreEntryId = Guid.NewGuid();
        var appliedByUserId = Guid.NewGuid();
        var penalty = Penalty.Create(scoreEntryId, "Unsportsmanlike conduct", appliedByUserId);
        var penaltyEntry = ScoreEntry.Penalty(
            scoreEntryId,
            liveSessionId,
            teamId,
            string.Empty,
            "Unsportsmanlike conduct",
            ScoreValue.Create(100),
            penalty.PenaltyId,
            penalty.AppliedAt);

        await using (var penaltyContext = _contextFactory.Create())
        {
            var scoreEntryRepository = new ScoreEntryRepository(penaltyContext);
            var penaltyRepository = new PenaltyRepository(penaltyContext);

            await scoreEntryRepository.AddAsync(penaltyEntry, CancellationToken.None);
            await penaltyRepository.AddAsync(penalty, CancellationToken.None);
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbral_backendDb"] = _connectionString,
            ["RabbitMq:HostName"] = "localhost",
            ["RabbitMq:Port"] = "5672",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:UserName"] = "guest",
            ["RabbitMq:Password"] = "guest"
        });

        builder.AddApplicationServices();
        builder.AddInfrastructureServices();
        builder.Services.AddSingleton<ICurrentUser>(StubCurrentUser.Default);
        builder.Services.AddSingleton<IRankingBroadcaster, NoOpRankingBroadcaster>();

        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var consumer = new ScoreEntryRegisteredConsumer(sender);
        var context = new Mock<ConsumeContext<ScoreEntryRegisteredIntegrationEvent>>();
        context.SetupGet(current => current.Message).Returns(new ScoreEntryRegisteredIntegrationEvent(
            penaltyEntry.ScoreEntryId,
            liveSessionId,
            teamId,
            ScoreEntryType.Penalty,
            penaltyEntry.ReasonCode,
            penaltyEntry.ScoreValue.Value,
            penaltyEntry.RecordedAt,
            penaltyEntry.SourceEntityType,
            penaltyEntry.SourceEntityId,
            penaltyEntry.RecordedByUserId));
        context.SetupGet(current => current.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        await using var verificationContext = _contextFactory.Create();
        var ranking = await new RankingRepository(verificationContext)
            .GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        ranking.Should().NotBeNull();
        ranking!.CalculationVersion.Should().Be(2);
        ranking.Rows.Should().ContainSingle();

        var row = ranking.Rows.Single();
        row.TeamId.Should().Be(teamId);
        row.TotalScore.Should().Be(0);
        row.TeamDisplayName.Should().Be("Gilded Owls");
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

        public static readonly StubCurrentUser Default = new();
    }
}
