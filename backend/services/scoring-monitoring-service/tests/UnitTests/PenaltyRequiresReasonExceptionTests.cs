using umbral_backend.Domain.Exceptions;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class PenaltyRequiresReasonExceptionTests
{
    [Fact]
    public void Exception_ShouldCaptureAttemptedReason()
    {
        var exception = new PenaltyRequiresReasonException("");

        exception.AttemptedReason.Should().Be("");
    }

    [Fact]
    public void Exception_ShouldCaptureNullReason()
    {
        var exception = new PenaltyRequiresReasonException(null);

        exception.AttemptedReason.Should().BeNull();
    }

    [Fact]
    public void Category_ShouldBeValidation()
    {
        var exception = new PenaltyRequiresReasonException(" ");

        exception.Category.Should().Be(ErrorCategory.Validation);
    }
}
