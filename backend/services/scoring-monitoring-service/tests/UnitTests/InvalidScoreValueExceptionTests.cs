using umbral_backend.Domain.Exceptions;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class InvalidScoreValueExceptionTests
{
    [Fact]
    public void Exception_ShouldCaptureAttemptedValue()
    {
        var exception = new InvalidScoreValueException(-12);

        exception.AttemptedValue.Should().Be(-12);
    }

    [Fact]
    public void Category_ShouldBeValidation()
    {
        var exception = new InvalidScoreValueException(0);

        exception.Category.Should().Be(ErrorCategory.Validation);
    }
}
