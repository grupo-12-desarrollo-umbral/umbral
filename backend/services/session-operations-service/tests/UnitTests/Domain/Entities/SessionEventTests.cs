using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class SessionEventTests
{
    [Fact]
    public void ForStateChange_CreatesAppendOnlyAuditRecord()
    {
        var liveSessionId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);

        var sessionEvent = SessionEvent.ForStateChange(
            liveSessionId,
            SessionState.Scheduled,
            SessionState.Preparing,
            occurredAt,
            SessionEventActorType.Operator,
            actorId: 27,
            reason: "  ready  ");

        sessionEvent.SessionEventId.Should().NotBeEmpty();
        sessionEvent.LiveSessionId.Should().Be(liveSessionId);
        sessionEvent.OccurredAt.Should().Be(occurredAt);
        sessionEvent.ActorType.Should().Be(SessionEventActorType.Operator);
        sessionEvent.ActorId.Should().Be(27);
        sessionEvent.EventType.Should().Be("SessionStateChanged");
        sessionEvent.PayloadSummary.Should().Be("Scheduled→Preparing: ready");
        sessionEvent.CorrelationId.Should().NotBeEmpty();
    }

    [Fact]
    public void ForOperativeClueAdded_CreatesAppendOnlyAuditRecord()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        const string clueText = "Check beneath the clock.";

        var sessionEvent = SessionEvent.ForOperativeClueAdded(
            liveSessionId,
            occurredAt,
            createdByUserId: 42,
            teamId,
            clueText);

        sessionEvent.SessionEventId.Should().NotBeEmpty();
        sessionEvent.LiveSessionId.Should().Be(liveSessionId);
        sessionEvent.OccurredAt.Should().Be(occurredAt);
        sessionEvent.ActorType.Should().Be(SessionEventActorType.Operator);
        sessionEvent.ActorId.Should().Be(42);
        sessionEvent.EventType.Should().Be("OperativeClueCreated");
        sessionEvent.PayloadSummary.Should().Contain("Check beneath the clock.");
        sessionEvent.PayloadSummary.Should().Contain(teamId.ToString());
        sessionEvent.CorrelationId.Should().NotBeEmpty();
    }

    [Fact]
    public void ForOperativeClueAdded_TruncatesVeryLongClueText()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        var longClueText = new string('x', 150);

        var sessionEvent = SessionEvent.ForOperativeClueAdded(
            liveSessionId,
            occurredAt,
            createdByUserId: 42,
            teamId,
            longClueText);

        // 150-char text truncated to 120 + ellipsis, plus prefix and team suffix
        sessionEvent.PayloadSummary.Should().Contain(new string('x', 20));
        // Truncated clue should not contain the full original text
        sessionEvent.PayloadSummary.Should().NotContain(longClueText);
    }
}
