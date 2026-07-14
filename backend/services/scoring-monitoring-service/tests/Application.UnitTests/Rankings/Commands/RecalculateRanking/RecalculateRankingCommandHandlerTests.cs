using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Rankings.Commands.RecalculateRanking;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Rankings.Commands.RecalculateRanking;

public sealed class RecalculateRankingCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesOrderedSnapshotUsingInjectedRankingPolicy()
    {
        var liveSessionId = Guid.NewGuid();
        var teamFast = Guid.NewGuid();
        var teamSlow = Guid.NewGuid();
        var scoreEntries = new[]
        {
            CreateGrant(liveSessionId, teamSlow, 100, Guid.NewGuid(), new DateTimeOffset(2026, 7, 14, 18, 0, 0, TimeSpan.Zero)),
            CreateGrant(liveSessionId, teamFast, 100, Guid.NewGuid(), new DateTimeOffset(2026, 7, 14, 18, 1, 0, TimeSpan.Zero))
        };

        var scoreEntryRepository = new Mock<IScoreEntryRepository>();
        scoreEntryRepository
            .Setup(repo => repo.ListByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scoreEntries);

        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetResolutionTimesByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResolutionTime>
            {
                [teamFast] = ResolutionTime.Comparable(TimeSpan.FromSeconds(30)),
                [teamSlow] = ResolutionTime.Comparable(TimeSpan.FromSeconds(45))
            });
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ranking?)null);

        Ranking? savedRanking = null;
        rankingRepository
            .Setup(repo => repo.SaveAsync(It.IsAny<Ranking>(), It.IsAny<CancellationToken>()))
            .Callback<Ranking, CancellationToken>((ranking, _) => savedRanking = ranking)
            .Returns(Task.CompletedTask);

        var handler = new RecalculateRankingCommandHandler(
            scoreEntryRepository.Object,
            rankingRepository.Object,
            new ResolutionTimeRankingPolicy());

        await handler.Handle(
            new RecalculateRankingCommand(
                liveSessionId,
                new DateTimeOffset(2026, 7, 14, 18, 5, 0, TimeSpan.Zero)),
            CancellationToken.None);

        savedRanking.Should().NotBeNull();
        savedRanking!.Rows.Select(row => (row.TeamId, row.Position)).Should().ContainInOrder(
            (teamFast, 1),
            (teamSlow, 2));
        savedRanking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RankingRefreshed>();
    }

    [Fact]
    public async Task Handle_WhenRankingAlreadyExists_IncrementsCalculationVersion()
    {
        var liveSessionId = Guid.NewGuid();
        var existingRanking = Ranking.Create(
            liveSessionId,
            Array.Empty<ScoreEntry>(),
            new Dictionary<Guid, ResolutionTime>(),
            new DateTimeOffset(2026, 7, 14, 17, 0, 0, TimeSpan.Zero),
            calculationVersion: 4,
            new ResolutionTimeRankingPolicy());
        existingRanking.ClearDomainEvents();

        var scoreEntryRepository = new Mock<IScoreEntryRepository>();
        scoreEntryRepository
            .Setup(repo => repo.ListByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreEntry>());

        var rankingRepository = new Mock<IRankingRepository>();
        rankingRepository
            .Setup(repo => repo.GetResolutionTimesByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ResolutionTime>());
        rankingRepository
            .Setup(repo => repo.GetByLiveSessionIdAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRanking);

        var handler = new RecalculateRankingCommandHandler(
            scoreEntryRepository.Object,
            rankingRepository.Object,
            new ResolutionTimeRankingPolicy());

        await handler.Handle(
            new RecalculateRankingCommand(
                liveSessionId,
                new DateTimeOffset(2026, 7, 14, 18, 15, 0, TimeSpan.Zero)),
            CancellationToken.None);

        existingRanking.CalculationVersion.Should().Be(5);
        rankingRepository.Verify(repo => repo.SaveAsync(existingRanking, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ScoreEntry CreateGrant(
        Guid liveSessionId,
        Guid teamId,
        int scoreValue,
        Guid sourceEntityId,
        DateTimeOffset recordedAt)
    {
        return ScoreEntry.Grant(
            liveSessionId,
            teamId,
            "trivia-answer-correct",
            ScoreValue.Create(scoreValue),
            recordedAt,
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId);
    }
}
