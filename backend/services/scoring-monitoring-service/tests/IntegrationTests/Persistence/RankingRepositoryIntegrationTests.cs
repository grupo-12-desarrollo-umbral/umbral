using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Persistence.Repositories;

namespace umbral_backend.ScoringMonitoring.IntegrationTests.Persistence;

[Collection(PostgreSqlCollection.Name)]
public sealed class RankingRepositoryIntegrationTests
{
    private readonly PersistenceTestContextFactory _contextFactory;
    private readonly IRankingPolicy _rankingPolicy = new ResolutionTimeRankingPolicy();

    public RankingRepositoryIntegrationTests(PostgreSqlFixture fixture)
    {
        _contextFactory = new PersistenceTestContextFactory(fixture.ConnectionString);
    }

    [Fact]
    public async Task SaveAndRefresh_RoundTripsAndReplacesRankingSnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var teamAlpha = Guid.NewGuid();
        var teamBeta = Guid.NewGuid();

        var initialEntries = new[]
        {
            ScoreEntry.Grant(
                liveSessionId,
                teamAlpha,
                "trivia-answer-correct",
                ScoreValue.Create(200),
                DateTimeOffset.UtcNow,
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid()),
            ScoreEntry.Grant(
                liveSessionId,
                teamBeta,
                "trivia-answer-correct",
                ScoreValue.Create(100),
                DateTimeOffset.UtcNow.AddSeconds(1),
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid())
        };

        var resolutionTimes = new Dictionary<Guid, ResolutionTime>
        {
            [teamAlpha] = ResolutionTime.Comparable(TimeSpan.FromSeconds(30)),
            [teamBeta] = ResolutionTime.Comparable(TimeSpan.FromSeconds(45))
        };

        await using (var writeContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(writeContext);
            var ranking = Ranking.Create(
                liveSessionId,
                initialEntries,
                resolutionTimes,
                DateTimeOffset.UtcNow,
                calculationVersion: 1,
                _rankingPolicy);

            await repository.SaveAsync(ranking, CancellationToken.None);
        }

        Ranking rankingToRefresh;

        await using (var readContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(readContext);
            rankingToRefresh = (await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None))!;

            rankingToRefresh.Rows.Should().HaveCount(2);
            rankingToRefresh.Rows.Select(row => row.TeamId).Should().ContainInOrder(teamAlpha, teamBeta);
        }

        var refreshedEntries = new[]
        {
            ScoreEntry.Grant(
                liveSessionId,
                teamAlpha,
                "trivia-answer-correct",
                ScoreValue.Create(200),
                DateTimeOffset.UtcNow,
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid()),
            ScoreEntry.Grant(
                liveSessionId,
                teamBeta,
                "trivia-answer-correct",
                ScoreValue.Create(300),
                DateTimeOffset.UtcNow.AddSeconds(1),
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid())
        };

        await using (var refreshContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(refreshContext);
            var trackedRanking = (await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None))!;

            trackedRanking.Refresh(
                refreshedEntries,
                resolutionTimes,
                DateTimeOffset.UtcNow.AddMinutes(1),
                calculationVersion: 2,
                _rankingPolicy);

            await repository.SaveAsync(trackedRanking, CancellationToken.None);
        }

        await using (var finalContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(finalContext);
            var ranking = (await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None))!;

            ranking.CalculationVersion.Should().Be(2);
            ranking.Rows.Should().HaveCount(2);
            ranking.Rows.Select(row => row.TeamId).Should().ContainInOrder(teamBeta, teamAlpha);
            ranking.Rows.Select(row => row.TotalScore).Should().ContainInOrder(300, 200);
        }
    }
}
