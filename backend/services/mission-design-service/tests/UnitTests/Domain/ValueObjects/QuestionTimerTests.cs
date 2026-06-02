using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Domain.ValueObjects;

public class QuestionTimerTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(120)]
    public void Create_WithSecondsWithinRange_SetsValue(int seconds)
    {
        var timer = QuestionTimer.Create(seconds);

        timer.Seconds.Should().Be(seconds);
    }

    [Fact]
    public void Create_WithSecondsBelowMinimum_ThrowsPositiveException()
    {
        var act = () => QuestionTimer.Create(4);

        act.Should().Throw<QuestionTimerMustBePositiveException>();
    }

    [Fact]
    public void Create_WithSecondsAboveMaximum_ThrowsMaximumException()
    {
        var act = () => QuestionTimer.Create(121);

        act.Should().Throw<QuestionTimerExceedsMaximumException>();
    }
}
