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

        var sessionStart = new DateTimeOffset(2026, 7, 14, 18, 0, 0, TimeSpan.Zero);

        var initialEntries = new[]
        {
            ScoreEntry.Grant(
                liveSessionId,
                teamAlpha,
                "Team Alpha",
                "trivia-answer-correct",
                ScoreValue.Create(200),
                sessionStart,
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid()),
            ScoreEntry.Grant(
                liveSessionId,
                teamBeta,
                "Team Beta",
                "trivia-answer-correct",
                ScoreValue.Create(100),
                sessionStart.AddSeconds(30),
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid())
        };

        await using (var writeContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(writeContext);
            var ranking = Ranking.Create(
                liveSessionId,
                initialEntries,
                sessionStart.AddMinutes(1),
                calculationVersion: 1,
                _rankingPolicy);

            await repository.SaveAsync(ranking, CancellationToken.None);
        }

        await using (var readContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(readContext);
            var rankingToRefresh = (await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None))!;

            rankingToRefresh.Rows.Should().HaveCount(2);
            rankingToRefresh.Rows.Select(row => row.TeamId).Should().ContainInOrder(teamAlpha, teamBeta);

            // The ledger-derived resolution time must survive the round trip as a real value.
            // Before the fix this column could only ever be NULL.
            rankingToRefresh.Rows.Should().OnlyContain(row => row.ResolutionTime.IsComparable);
            rankingToRefresh.Rows.Single(row => row.TeamId == teamAlpha)
                .ResolutionTime.Value.Should().Be(TimeSpan.Zero);
            rankingToRefresh.Rows.Single(row => row.TeamId == teamBeta)
                .ResolutionTime.Value.Should().Be(TimeSpan.FromSeconds(30));
        }

        var refreshedEntries = new[]
        {
            ScoreEntry.Grant(
                liveSessionId,
                teamAlpha,
                "Team Alpha",
                "trivia-answer-correct",
                ScoreValue.Create(200),
                sessionStart,
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid()),
            ScoreEntry.Grant(
                liveSessionId,
                teamBeta,
                "Team Beta",
                "trivia-answer-correct",
                ScoreValue.Create(300),
                sessionStart.AddSeconds(30),
                Domain.Enums.ScoreSourceType.TriviaAnswerSubmission,
                Guid.NewGuid())
        };

        await using (var refreshContext = _contextFactory.Create())
        {
            var repository = new RankingRepository(refreshContext);
            var trackedRanking = (await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None))!;

            trackedRanking.Refresh(
                refreshedEntries,
                sessionStart.AddMinutes(1),
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
