using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class TriviaScorePolicyTests
{
    [Fact]
    public void Award_ReturnsAuthoredQuestionValueUnchanged()
    {
        var policy = new TriviaScorePolicy();

        // The difficulty factor is irrelevant to trivia; a non-base factor must not change the award.
        var awarded = policy.Award(ScoreValue.Create(250), difficultyFactor: 3);

        awarded.Value.Should().Be(250);
    }

    [Fact]
    public void TriviaScorePolicy_ShouldBeSealed()
    {
        typeof(TriviaScorePolicy).IsSealed.Should().BeTrue();
    }
}
