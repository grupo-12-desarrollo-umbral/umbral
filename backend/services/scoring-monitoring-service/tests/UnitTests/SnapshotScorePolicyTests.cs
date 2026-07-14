using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class SnapshotScorePolicyTests
{
    [Fact]
    public void Award_ShouldReturnAcceptedOutcomeSnapshot()
    {
        var policy = new SnapshotScorePolicy();
        var awarded = policy.Award(ScoreValue.Create(60));

        awarded.Value.Should().Be(60);
    }

    [Fact]
    public void SnapshotScorePolicy_ShouldBeSealed()
    {
        typeof(SnapshotScorePolicy).IsSealed.Should().BeTrue();
    }
}
