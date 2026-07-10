using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class ScoreValueTests
{
    [Fact]
    public void Create_WithValidPoints_StoresPoints()
    {
        var score = ScoreValue.Create(50);

        score.Points.Should().Be(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WhenNotPositive_Throws(int points)
    {
        var act = () => ScoreValue.Create(points);

        act.Should().Throw<ScoreValueMustBePositiveException>();
    }

    [Fact]
    public void Create_WhenAboveMaximum_Throws()
    {
        var act = () => ScoreValue.Create(ScoreValue.MaximumPoints + 1);

        act.Should().Throw<ScoreValueExceedsMaximumException>();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(25)]
    [InlineData(99)]
    public void Create_WhenNotAMultipleOfTen_Throws(int points)
    {
        var act = () => ScoreValue.Create(points);

        act.Should().Throw<ScoreValueMustBeMultipleOfTenException>();
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        ScoreValue.Create(30).Should().Be(ScoreValue.Create(30));
        ScoreValue.Create(30).Should().NotBe(ScoreValue.Create(40));
    }
}
