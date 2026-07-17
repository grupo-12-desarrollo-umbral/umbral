using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.ScoringMonitoring.UnitTests;

public sealed class SessionEventTests
{
    [Fact]
    public void ForStateChange_PreservesMinimumAuditTraceability()
    {
        var liveSessionId = Guid.NewGuid();
        var responsibleUserExternalId = Guid.NewGuid();
        var changedAt = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var sessionEvent = SessionEvent.ForStateChange(
            liveSessionId,
            SessionState.Scheduled,
            SessionState.Preparing,
            changedAt,
            responsibleUserExternalId,
            "  preparation started  ");

        sessionEvent.LiveSessionId.Should().Be(liveSessionId);
        sessionEvent.EventType.Should().Be(SessionEvent.StateChangedEventType);
        sessionEvent.OccurredAt.Should().Be(changedAt);
        sessionEvent.ResponsibleUserExternalId.Should().Be(responsibleUserExternalId);
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
        sessionEvent.ResponsibleUserExternalId.Should().BeNull();
        sessionEvent.PayloadSummary.Should().Be("Session results finalized");
    }

    [Fact]
    public void NewFactories_SameSourceFactProduceSameKey_AndDifferentTypesProduceDifferentKeys()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 7, 16, 14, 0, 0, TimeSpan.Zero);

        var evidence = SessionEvent.ForEvidenceSubmitted(
            liveSessionId,
            teamId,
            sourceId,
            "TriviaAnswer",
            occurredAt);
        var redelivery = SessionEvent.ForEvidenceSubmitted(
            liveSessionId,
            teamId,
            sourceId,
            "TriviaAnswer",
            occurredAt);
        var score = SessionEvent.ForScoreChanged(
            liveSessionId,
            teamId,
            sourceId,
            100,
            "trivia-answer-correct",
            occurredAt);

        redelivery.SourceEventKey.Should().Be(evidence.SourceEventKey);
        score.SourceEventKey.Should().NotBe(evidence.SourceEventKey);
        evidence.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void ForClueReleased_DifferentTeamsProduceDifferentSourceKeys()
    {
        var liveSessionId = Guid.NewGuid();
        var clueId = Guid.NewGuid();
        var releasedAt = DateTimeOffset.UtcNow;

        var firstTeam = SessionEvent.ForClueReleased(
            liveSessionId,
            Guid.NewGuid(),
            targetId: null,
            clueId,
            "Manual",
            releasedAt,
            responsibleUserExternalId: Guid.NewGuid());
        var secondTeam = SessionEvent.ForClueReleased(
            liveSessionId,
            Guid.NewGuid(),
            targetId: null,
            clueId,
            "Manual",
            releasedAt,
            responsibleUserExternalId: Guid.NewGuid());

        secondTeam.SourceEventKey.Should().NotBe(firstTeam.SourceEventKey);
    }
}
