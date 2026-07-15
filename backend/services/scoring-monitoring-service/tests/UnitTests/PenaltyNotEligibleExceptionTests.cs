using umbral_backend.Domain.Exceptions;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class PenaltyNotEligibleExceptionTests
{
    [Fact]
    public void Exception_ShouldCaptureContext()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        const string reason = "test reason";

        var exception = new PenaltyNotEligibleException(sessionId, teamId, reason);

        exception.LiveSessionId.Should().Be(sessionId);
        exception.TeamId.Should().Be(teamId);
        exception.Reason.Should().Be(reason);
    }

    [Fact]
    public void Category_ShouldBeConflict()
    {
        var exception = new PenaltyNotEligibleException(Guid.NewGuid(), Guid.NewGuid(), "reason");

        exception.Category.Should().Be(ErrorCategory.Conflict);
    }
}
