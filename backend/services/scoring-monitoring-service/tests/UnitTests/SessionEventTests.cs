using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class SessionEventTests
{
    [Fact]
    public void ForStateChange_PreservesMinimumAuditTraceability()
    {
        var liveSessionId = Guid.NewGuid();
        var changedAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var sessionEvent = SessionEvent.ForStateChange(
            liveSessionId,
            SessionState.Scheduled,
            SessionState.Preparing,
            changedAt,
            42,
            "  preparation started  ");

        sessionEvent.LiveSessionId.Should().Be(liveSessionId);
        sessionEvent.EventType.Should().Be(SessionEvent.StateChangedEventType);
        sessionEvent.OccurredAt.Should().Be(changedAt);
        sessionEvent.ResponsibleUserId.Should().Be(42);
        sessionEvent.PayloadSummary.Should().Be("Scheduled→Preparing: preparation started");
    }

    [Fact]
    public void Factories_SameSourceFact_ProduceSameIdempotencyKey()
    {
        var liveSessionId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var first = SessionEvent.ForQuestionClosed(liveSessionId, 3, occurredAt);
        var redelivery = SessionEvent.ForQuestionClosed(liveSessionId, 3, occurredAt);

        redelivery.SourceEventKey.Should().Be(first.SourceEventKey);
        redelivery.SessionEventId.Should().NotBe(first.SessionEventId);
    }

    [Fact]
    public void ForQuestionClosed_RejectsNegativeIndex()
    {
        var act = () => SessionEvent.ForQuestionClosed(Guid.NewGuid(), -1, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForResultsFinalized_RecordsSystemFact()
    {
        var finishedAt = DateTimeOffset.UtcNow;

        var sessionEvent = SessionEvent.ForResultsFinalized(Guid.NewGuid(), finishedAt);

        sessionEvent.EventType.Should().Be(SessionEvent.ResultsFinalizedEventType);
        sessionEvent.OccurredAt.Should().Be(finishedAt);
        sessionEvent.ResponsibleUserId.Should().BeNull();
        sessionEvent.PayloadSummary.Should().Be("Session results finalized");
    }
}
