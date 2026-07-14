using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ResolutionTimeRankingPolicyTests
{
    [Fact]
    public void Rank_ShouldOrderByDescendingTotal_ThenByResolutionTime()
    {
        var teamFast = Guid.NewGuid();
        var teamSlow = Guid.NewGuid();
        var teamLowScore = Guid.NewGuid();
        var policy = new ResolutionTimeRankingPolicy();

        var ranked = policy.Rank(
            new[]
            {
                (teamSlow, 100, ResolutionTime.Comparable(TimeSpan.FromSeconds(45))),
                (teamLowScore, 80, ResolutionTime.Comparable(TimeSpan.FromSeconds(10))),
                (teamFast, 100, ResolutionTime.Comparable(TimeSpan.FromSeconds(30)))
            });

        ranked.Select(row => (row.TeamId, row.Position)).Should().ContainInOrder(
            (teamFast, 1),
            (teamSlow, 2),
            (teamLowScore, 3));
    }

    [Fact]
    public void Rank_ShouldSharePositionForEqualOrNonComparableResolutionTimes()
    {
        var teamComparable = Guid.NewGuid();
        var teamNonComparable = Guid.NewGuid();
        var teamEqualComparable = Guid.NewGuid();
        var policy = new ResolutionTimeRankingPolicy();

        var ranked = policy.Rank(
            new[]
            {
                (teamComparable, 150, ResolutionTime.Comparable(TimeSpan.FromSeconds(20))),
                (teamNonComparable, 150, ResolutionTime.NonComparable()),
                (teamEqualComparable, 150, ResolutionTime.Comparable(TimeSpan.FromSeconds(20)))
            });

        ranked.Should().HaveCount(3);
        ranked[0].Position.Should().Be(1);
        ranked[1].Position.Should().Be(1);
        ranked[2].Position.Should().Be(1);
    }

    [Fact]
    public void ResolutionTimeRankingPolicy_ShouldBeSealed()
    {
        typeof(ResolutionTimeRankingPolicy).IsSealed.Should().BeTrue();
    }
}
