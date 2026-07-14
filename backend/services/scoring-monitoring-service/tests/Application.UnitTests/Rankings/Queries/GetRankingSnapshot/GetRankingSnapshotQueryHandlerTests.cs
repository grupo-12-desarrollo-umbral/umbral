using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Queries.GetRankingSnapshot;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Rankings.Queries.GetRankingSnapshot;

public sealed class GetRankingSnapshotQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenRankingExists_ReturnsOrderedSnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var ranking = Ranking.Create(
            liveSessionId,
            new[]
            {
                ScoreEntry.Grant(
                    liveSessionId,
                    teamId,
                    "trivia-answer-correct",
                    ScoreValue.Create(100),
                    new DateTimeOffset(2026, 7, 14, 17, 0, 0, TimeSpan.Zero),
                    ScoreSourceType.TriviaAnswerSubmission,
                    Guid.NewGuid())
            },
            new Dictionary<Guid, ResolutionTime>(),
            new DateTimeOffset(2026, 7, 14, 17, 5, 0, TimeSpan.Zero),
            calculationVersion: 2,
            new ResolutionTimeRankingPolicy());

        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ranking);

        var handler = new GetRankingSnapshotQueryHandler(rankingRepository.Object);

        var snapshot = await handler.Handle(new GetRankingSnapshotQuery(liveSessionId), CancellationToken.None);

        snapshot.LiveSessionId.Should().Be(liveSessionId);
        snapshot.CalculationVersion.Should().Be(2);
        snapshot.Rows.Should().ContainSingle(row => row.TeamId == teamId && row.Position == 1 && row.TotalScore == 100);
    }

    [Fact]
    public async Task Handle_WhenRankingDoesNotExist_ReturnsEmptySnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ranking?)null);

        var handler = new GetRankingSnapshotQueryHandler(rankingRepository.Object);

        var snapshot = await handler.Handle(new GetRankingSnapshotQuery(liveSessionId), CancellationToken.None);

        snapshot.LiveSessionId.Should().Be(liveSessionId);
        snapshot.Rows.Should().BeEmpty();
    }
}
