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
}
