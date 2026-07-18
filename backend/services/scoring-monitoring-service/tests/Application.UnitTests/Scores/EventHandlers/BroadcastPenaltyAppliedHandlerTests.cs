using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.EventHandlers;
using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.Application.UnitTests.Scores.EventHandlers;

public sealed class BroadcastPenaltyAppliedHandlerTests
{
    [Fact]
    public async Task Handle_BroadcastsNotification_WithEventFieldsPreserved()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var penaltyId = Guid.NewGuid();
        var scoreEntryId = Guid.NewGuid();
        var appliedAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

        var penaltyBroadcaster = new Mock<IPenaltyBroadcaster>();
        var handler = new BroadcastPenaltyAppliedHandler(penaltyBroadcaster.Object);

        await handler.Handle(
            new PenaltyApplied(penaltyId, scoreEntryId, liveSessionId, teamId, 100, "Unsportsmanlike conduct", appliedAt),
            CancellationToken.None);

        penaltyBroadcaster.Verify(
            broadcaster => broadcaster.PenaltyApplied(
                liveSessionId,
                It.Is<PenaltyAppliedNotificationDto>(dto =>
                    dto.LiveSessionId == liveSessionId &&
                    dto.TeamId == teamId &&
                    dto.PenaltyId == penaltyId &&
                    dto.ScoreEntryId == scoreEntryId &&
                    dto.DeductionMagnitude == 100 &&
                    dto.Reason == "Unsportsmanlike conduct" &&
                    dto.AppliedAt == appliedAt),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
