using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.EventHandlers;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Rankings.EventHandlers;

public sealed class BroadcastRankingRefreshedHandlerTests
{
    [Fact]
    public async Task Handle_WhenRankingExists_BroadcastsSnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var ranking = Ranking.Create(
            liveSessionId,
            new[]
            {
                ScoreEntry.Grant(
                    liveSessionId,
                    Guid.NewGuid(),
                    "trivia-answer-correct",
                    ScoreValue.Create(100),
                    new DateTimeOffset(2026, 7, 14, 17, 0, 0, TimeSpan.Zero),
                    ScoreSourceType.TriviaAnswerSubmission,
                    Guid.NewGuid())
            },
            new DateTimeOffset(2026, 7, 14, 17, 5, 0, TimeSpan.Zero),
            calculationVersion: 1,
            new ResolutionTimeRankingPolicy());

        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ranking);

        var rankingBroadcaster = new Mock<IRankingBroadcaster>();
        var handler = new BroadcastRankingRefreshedHandler(rankingRepository.Object, rankingBroadcaster.Object);

        await handler.Handle(
            new RankingRefreshed(ranking.RankingId, liveSessionId, ranking.GeneratedAt, ranking.CalculationVersion),
            CancellationToken.None);

        rankingBroadcaster.Verify(
            broadcaster => broadcaster.RankingChanged(
                liveSessionId,
                It.Is<RankingSnapshotDto>(snapshot => snapshot.LiveSessionId == liveSessionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRankingDoesNotExist_DoesNotBroadcast()
    {
        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ranking?)null);

        var rankingBroadcaster = new Mock<IRankingBroadcaster>();
        var handler = new BroadcastRankingRefreshedHandler(rankingRepository.Object, rankingBroadcaster.Object);

        await handler.Handle(
            new RankingRefreshed(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, 1),
            CancellationToken.None);

        rankingBroadcaster.Verify(
            broadcaster => broadcaster.RankingChanged(
                It.IsAny<Guid>(),
                It.IsAny<RankingSnapshotDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
