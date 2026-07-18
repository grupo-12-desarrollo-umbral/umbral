using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class PenaltyScorePolicyTests
{
    [Fact]
    public void Award_ReturnsDeductionMagnitudeUnchanged()
    {
        var policy = new PenaltyScorePolicy();

        var awarded = policy.Award(ScoreValue.Create(100), difficultyFactor: 3);

        awarded.Value.Should().Be(100);
    }

    [Fact]
    public void PenaltyScorePolicy_ShouldBeSealed()
    {
        typeof(PenaltyScorePolicy).IsSealed.Should().BeTrue();
    }
}
