using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class PenaltyAppliedTests
{
    [Fact]
    public void Constructor_ShouldExposeEventPayload()
    {
        var penaltyId = Guid.NewGuid();
        var scoreEntryId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var appliedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);
        var appliedByUserId = Guid.NewGuid();
        const string reason = "Unsportsmanlike conduct";
        const int magnitude = 50;

        var @event = new PenaltyApplied(
            penaltyId,
            scoreEntryId,
            liveSessionId,
            teamId,
            magnitude,
            appliedAt,
            appliedByUserId,
            reason);

        @event.PenaltyId.Should().Be(penaltyId);
        @event.ScoreEntryId.Should().Be(scoreEntryId);
        @event.LiveSessionId.Should().Be(liveSessionId);
        @event.TeamId.Should().Be(teamId);
        @event.DeductionMagnitude.Should().Be(magnitude);
        @event.AppliedAt.Should().Be(appliedAt);
        @event.AppliedByUserId.Should().Be(appliedByUserId);
        @event.Reason.Should().Be(reason);
    }
}
