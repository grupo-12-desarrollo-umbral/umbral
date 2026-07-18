using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class QuestionTimerTests
{
    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    public void Create_WithSecondsWithinRange_SetsValue(int seconds)
    {
        var timer = QuestionTimer.Create(seconds);

        timer.Seconds.Should().Be(seconds);
    }

    [Fact]
    public void Create_WithSecondsBelowMinimum_ThrowsPositiveException()
    {
        var act = () => QuestionTimer.Create(14);

        act.Should().Throw<QuestionTimerMustBePositiveException>();
    }

    [Fact]
    public void Create_WithSecondsAboveMaximum_ThrowsMaximumException()
    {
        var act = () => QuestionTimer.Create(31);

        act.Should().Throw<QuestionTimerExceedsMaximumException>();
    }
}
