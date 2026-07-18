using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class TargetScorePolicyTests
{
    [Theory]
    [InlineData(1, 50)]   // Beginner     (base 50 × 1)
    [InlineData(2, 100)]  // Intermediate (base 50 × 2)
    [InlineData(3, 150)]  // Advanced     (base 50 × 3)
    public void Award_WeightsBaseScoreByDifficultyFactor(int difficultyFactor, int expected)
    {
        var policy = new TargetScorePolicy();

        var awarded = policy.Award(ScoreValue.Create(0), difficultyFactor);

        awarded.Value.Should().Be(expected);
    }

    [Fact]
    public void Award_IgnoresAcceptedOutcomeValue_TargetScoreIsDifficultyDefinedNotAuthored()
    {
        var policy = new TargetScorePolicy();

        // Whatever value the resolution carries, a target's award is fixed by base × difficulty.
        policy.Award(ScoreValue.Create(999), 2).Value.Should().Be(100);
        policy.Award(ScoreValue.Create(7), 2).Value.Should().Be(100);
    }

    [Fact]
    public void Award_DiffersFromTriviaPolicyForSameInput_SoRuntimeSelectionChangesResult()
    {
        var input = ScoreValue.Create(50);

        var target = new TargetScorePolicy().Award(input, difficultyFactor: 3);
        var trivia = new TriviaScorePolicy().Award(input, difficultyFactor: 3);

        target.Value.Should().Be(150);
        trivia.Value.Should().Be(50);
        target.Should().NotBe(trivia);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Award_WhenDifficultyFactorIsNotPositive_Throws(int difficultyFactor)
    {
        var policy = new TargetScorePolicy();

        var act = () => policy.Award(ScoreValue.Create(50), difficultyFactor);

        act.Should().Throw<InvalidDifficultyFactorException>();
    }

    [Fact]
    public void TargetScorePolicy_ShouldBeSealed()
    {
        typeof(TargetScorePolicy).IsSealed.Should().BeTrue();
    }
}
