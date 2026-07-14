using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class RankingTests
{
    [Fact]
    public void Create_ShouldDeriveTeamTotalsByFoldingEntries_AndRaiseRankingRefreshed()
    {
        var liveSessionId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var entries = new[]
        {
            CreateGrant(liveSessionId, teamA, 100, Guid.NewGuid()),
            CreateGrant(liveSessionId, teamA, 50, Guid.NewGuid()),
            CreateGrant(liveSessionId, teamB, 120, Guid.NewGuid())
        };

        var ranking = Ranking.Create(
            liveSessionId,
            entries,
            new Dictionary<Guid, ResolutionTime>
            {
                [teamA] = ResolutionTime.Comparable(TimeSpan.FromSeconds(40)),
                [teamB] = ResolutionTime.Comparable(TimeSpan.FromSeconds(45))
            },
            new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.Zero),
            calculationVersion: 1,
            new ResolutionTimeRankingPolicy());

        ranking.Rows.Should().HaveCount(2);
        ranking.Rows.Select(row => (row.TeamId, row.TotalScore)).Should().ContainInOrder(
            (teamA, 150),
            (teamB, 120));
        ranking.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RankingRefreshed>();
    }

    private static ScoreEntry CreateGrant(Guid liveSessionId, Guid teamId, int scoreValue, Guid sourceEntityId)
    {
        return ScoreEntry.Grant(
            liveSessionId,
            teamId,
            "grant",
            ScoreValue.Create(scoreValue),
            DateTimeOffset.UtcNow,
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId);
    }
}
