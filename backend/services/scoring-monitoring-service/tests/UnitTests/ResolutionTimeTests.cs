using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ResolutionTimeTests
{
    [Fact]
    public void Comparable_ShouldRejectNegativeDuration()
    {
        var act = () => ResolutionTime.Comparable(TimeSpan.FromSeconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NonComparable_ShouldCompareAsSharedRankCandidate()
    {
        var left = ResolutionTime.NonComparable();
        var right = ResolutionTime.NonComparable();

        left.CompareTo(right).Should().Be(0);
        left.Should().Be(right);
    }
}
