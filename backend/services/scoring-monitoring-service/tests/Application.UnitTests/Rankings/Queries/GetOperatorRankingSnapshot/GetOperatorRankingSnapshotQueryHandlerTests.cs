using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Queries.GetOperatorRankingSnapshot;
using umbral_backend.Application.Scores.Common.Authorization;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Rankings.Queries.GetOperatorRankingSnapshot;

public sealed class GetOperatorRankingSnapshotQueryHandlerTests
{
    private static Ranking BuildRanking(Guid liveSessionId, Guid teamId)
    {
        return Ranking.Create(
            liveSessionId,
            new[]
            {
                ScoreEntry.Grant(
                    liveSessionId,
                    teamId,
                    "Team A",
                    "trivia-answer-correct",
                    ScoreValue.Create(100),
                    new DateTimeOffset(2026, 7, 14, 17, 0, 0, TimeSpan.Zero),
                    ScoreSourceType.TriviaAnswerSubmission,
                    Guid.NewGuid())
            },
            new DateTimeOffset(2026, 7, 14, 17, 5, 0, TimeSpan.Zero),
            calculationVersion: 2,
            new ResolutionTimeRankingPolicy());
    }

    [Fact]
    public async Task Handle_WhenAccessAllowedAndRankingExists_ReturnsSessionWideSnapshot()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildRanking(liveSessionId, teamId));

        var accessResolver = new Mock<IScoringSessionAccessResolver>();
        accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GetOperatorRankingSnapshotQueryHandler(rankingRepository.Object, accessResolver.Object);

        var snapshot = await handler.Handle(
            new GetOperatorRankingSnapshotQuery(liveSessionId),
            CancellationToken.None);

        snapshot.LiveSessionId.Should().Be(liveSessionId);
        snapshot.CalculationVersion.Should().Be(2);
        snapshot.Rows.Should().ContainSingle(row => row.TeamId == teamId && row.Position == 1 && row.TotalScore == 100);
    }

    [Fact]
    public async Task Handle_WhenAccessAllowedAndRankingDoesNotExist_ReturnsEmptySnapshot()
    {
        var liveSessionId = Guid.NewGuid();

        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ranking?)null);

        var accessResolver = new Mock<IScoringSessionAccessResolver>();
        accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new GetOperatorRankingSnapshotQueryHandler(rankingRepository.Object, accessResolver.Object);

        var snapshot = await handler.Handle(
            new GetOperatorRankingSnapshotQuery(liveSessionId),
            CancellationToken.None);

        snapshot.LiveSessionId.Should().Be(liveSessionId);
        snapshot.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAccessDenied_ThrowsForbiddenAndDoesNotQueryRanking()
    {
        var liveSessionId = Guid.NewGuid();

        var rankingRepository = new Mock<IRankingRepository>();

        var accessResolver = new Mock<IScoringSessionAccessResolver>();
        accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var handler = new GetOperatorRankingSnapshotQueryHandler(rankingRepository.Object, accessResolver.Object);

        var act = async () => await handler.Handle(
            new GetOperatorRankingSnapshotQuery(liveSessionId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        rankingRepository.Verify(
            repo => repo.GetByLiveSessionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
