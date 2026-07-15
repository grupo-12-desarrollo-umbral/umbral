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

        var sessionStart = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

        var entries = new[]
        {
            CreateGrant(liveSessionId, teamA, 100, Guid.NewGuid(), sessionStart),
            CreateGrant(liveSessionId, teamA, 50, Guid.NewGuid(), sessionStart.AddSeconds(40)),
            CreateGrant(liveSessionId, teamB, 120, Guid.NewGuid(), sessionStart.AddSeconds(45))
        };

        var ranking = Ranking.Create(
            liveSessionId,
            entries,
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

    [Fact]
    public void Refresh_ShouldDeriveResolutionTimeFromLedger_RelativeToEarliestEntryInSession()
    {
        var liveSessionId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var sessionStart = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

        var entries = new[]
        {
            CreateGrant(liveSessionId, teamA, 100, Guid.NewGuid(), sessionStart),
            CreateGrant(liveSessionId, teamB, 100, Guid.NewGuid(), sessionStart.AddSeconds(90))
        };

        var ranking = Ranking.Create(
            liveSessionId,
            entries,
            new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.Zero),
            calculationVersion: 1,
            new ResolutionTimeRankingPolicy());

        // Elapsed from the session's earliest ledger entry, not a hand-injected value.
        ranking.Rows.Single(row => row.TeamId == teamA).ResolutionTime.Value.Should().Be(TimeSpan.Zero);
        ranking.Rows.Single(row => row.TeamId == teamB).ResolutionTime.Value.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void Refresh_ShouldIgnoreEntriesFromOtherSessions_WhenDerivingResolutionTime()
    {
        var liveSessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var sessionStart = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);

        var entries = new[]
        {
            // Earlier, but belongs to a different session: must not become the baseline.
            CreateGrant(otherSessionId, teamA, 100, Guid.NewGuid(), sessionStart.AddSeconds(-500)),
            CreateGrant(liveSessionId, teamA, 100, Guid.NewGuid(), sessionStart)
        };

        var ranking = Ranking.Create(
            liveSessionId,
            entries,
            new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.Zero),
            calculationVersion: 1,
            new ResolutionTimeRankingPolicy());

        ranking.Rows.Should().ContainSingle();
        ranking.Rows.Single().ResolutionTime.Value.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Refresh_ShouldProduceNoRows_WhenSessionHasNoEntries()
    {
        var liveSessionId = Guid.NewGuid();

        var ranking = Ranking.Create(
            liveSessionId,
            Array.Empty<ScoreEntry>(),
            new DateTimeOffset(2026, 7, 14, 12, 30, 0, TimeSpan.Zero),
            calculationVersion: 1,
            new ResolutionTimeRankingPolicy());

        ranking.Rows.Should().BeEmpty();
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
            "Team A",
            "grant",
            ScoreValue.Create(scoreValue),
            recordedAt,
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId);
    }
}
