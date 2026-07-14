using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class ScoreEntryRegisteredTests
{
    [Fact]
    public void Constructor_ShouldExposeEventPayload()
    {
        var scoreEntryId = Guid.NewGuid();
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var sourceEntityId = Guid.NewGuid();
        var recordedAt = new DateTimeOffset(2026, 7, 14, 13, 0, 0, TimeSpan.Zero);

        var @event = new ScoreEntryRegistered(
            scoreEntryId,
            liveSessionId,
            teamId,
            ScoreEntryType.Grant,
            "trivia-correct-answer",
            25,
            recordedAt,
            ScoreSourceType.TriviaAnswerSubmission,
            sourceEntityId,
            41);

        @event.ScoreEntryId.Should().Be(scoreEntryId);
        @event.LiveSessionId.Should().Be(liveSessionId);
        @event.TeamId.Should().Be(teamId);
        @event.SourceEntityId.Should().Be(sourceEntityId);
        @event.RecordedByUserId.Should().Be(41);
    }
}
