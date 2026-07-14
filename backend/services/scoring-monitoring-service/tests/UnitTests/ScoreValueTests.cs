using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScoreValueTests
{
    [Fact]
    public void Create_ShouldRejectNegativeValues()
    {
        var act = () => ScoreValue.Create(-1);

        act.Should().Throw<InvalidScoreValueException>()
            .Which.AttemptedValue.Should().Be(-1);
    }

    [Fact]
    public void Create_ShouldAllowZeroAndPositiveValues()
    {
        ScoreValue.Create(0).Value.Should().Be(0);
        ScoreValue.Create(25).Value.Should().Be(25);
    }
}
