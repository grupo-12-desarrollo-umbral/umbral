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
}
