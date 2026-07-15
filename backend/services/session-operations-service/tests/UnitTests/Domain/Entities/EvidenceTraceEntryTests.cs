using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class EvidenceTraceEntryTests
{
    [Fact]
    public void ForRegistration_CreatesEntryWithPendingStateAndContextFields()
    {
        var submissionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var substageId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);

        var entry = EvidenceTraceEntry.ForRegistration(
            submissionId,
            sessionId,
            teamId,
            substageId,
            EvidenceSubmissionType.TriviaAnswer,
            participantId,
            "question:1",
            submittedAt);

        entry.EvidenceSubmissionId.Should().Be(submissionId);
        entry.LiveSessionId.Should().Be(sessionId);
        entry.TeamId.Should().Be(teamId);
        entry.ActiveSubstageId.Should().Be(substageId);
        entry.SubmissionType.Should().Be(EvidenceSubmissionType.TriviaAnswer);
        entry.SubmittedByParticipantId.Should().Be(participantId);
        entry.OriginReference.Should().Be("question:1");
        entry.SubmittedAt.Should().Be(submittedAt);
        entry.ValidationState.Should().Be(EvidenceValidationState.Pending);
        entry.RejectionReason.Should().BeNull();
        entry.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void ForRegistration_WithNullOrigin_RecordsNullOrigin()
    {
        var entry = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            null,
            null,
            DateTimeOffset.UtcNow);

        entry.OriginReference.Should().BeNull();
        entry.SubmittedByParticipantId.Should().BeNull();
    }

    [Fact]
    public void MarkAccepted_SetsAcceptedAndResolvedAt()
    {
        var entry = NewPendingEntry();
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);

        entry.MarkAccepted(resolvedAt);

        entry.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        entry.ResolvedAt.Should().Be(resolvedAt);
        entry.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void MarkAccepted_WhenAlreadyAccepted_IsIdempotent()
    {
        var entry = NewPendingEntry();
        var firstResolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        entry.MarkAccepted(firstResolvedAt);

        var secondResolvedAt = new DateTimeOffset(2026, 7, 14, 11, 0, 0, TimeSpan.Zero);
        entry.MarkAccepted(secondResolvedAt);

        entry.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        entry.ResolvedAt.Should().Be(firstResolvedAt);
    }

    [Fact]
    public void MarkRejected_RecordsReasonAndResolvedAt()
    {
        var entry = NewPendingEntry();
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);

        entry.MarkRejected("The scanned value does not resolve to a target.", resolvedAt);

        entry.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        entry.RejectionReason.Should().Be("The scanned value does not resolve to a target.");
        entry.ResolvedAt.Should().Be(resolvedAt);
    }

    [Fact]
    public void MarkRejected_WhenAlreadyRejected_IsIdempotent()
    {
        var entry = NewPendingEntry();
        var firstResolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        entry.MarkRejected("first reason", firstResolvedAt);

        var secondResolvedAt = new DateTimeOffset(2026, 7, 14, 11, 0, 0, TimeSpan.Zero);
        entry.MarkRejected("second reason", secondResolvedAt);

        entry.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        entry.RejectionReason.Should().Be("first reason");
        entry.ResolvedAt.Should().Be(firstResolvedAt);
    }

    [Fact]
    public void MarkRejected_AfterAccepted_StillApplies()
    {
        var entry = NewPendingEntry();
        var acceptedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        entry.MarkAccepted(acceptedAt);

        var rejectedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 6, TimeSpan.Zero);
        entry.MarkRejected("overturned", rejectedAt);

        entry.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        entry.RejectionReason.Should().Be("overturned");
        entry.ResolvedAt.Should().Be(rejectedAt);
    }

    [Fact]
    public void MarkAccepted_AfterRejected_StillApplies()
    {
        var entry = NewPendingEntry();
        var rejectedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        entry.MarkRejected("rejected first", rejectedAt);

        var acceptedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 6, TimeSpan.Zero);
        entry.MarkAccepted(acceptedAt);

        entry.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        entry.RejectionReason.Should().BeNull();
        entry.ResolvedAt.Should().Be(acceptedAt);
    }

    [Fact]
    public void MergeFrom_FillsUnknownContextWithoutDisturbingTerminalState()
    {
        // A resolution-first stub: terminal, but with no participant/origin context.
        var stub = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            submittedByParticipantId: null,
            originReference: null,
            DateTimeOffset.UtcNow);
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        stub.MarkAccepted(resolvedAt);

        var registration = NewPendingEntry();

        stub.MergeFrom(registration);

        stub.SubmittedByParticipantId.Should().Be(registration.SubmittedByParticipantId);
        stub.OriginReference.Should().Be("question:1");
        stub.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        stub.ResolvedAt.Should().Be(resolvedAt);
    }

    [Fact]
    public void MergeFrom_AdoptsTerminalStateWhenStillPending()
    {
        var registration = NewPendingEntry();

        var resolution = NewPendingEntry();
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        resolution.MarkRejected("blurry photo", resolvedAt);

        registration.MergeFrom(resolution);

        registration.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        registration.RejectionReason.Should().Be("blurry photo");
        registration.ResolvedAt.Should().Be(resolvedAt);
    }

    [Fact]
    public void MergeFrom_DoesNotOverwriteKnownContext()
    {
        var entry = NewPendingEntry();
        var originalParticipantId = entry.SubmittedByParticipantId;

        var other = NewPendingEntry();

        entry.MergeFrom(other);

        entry.SubmittedByParticipantId.Should().Be(
            originalParticipantId, "context already known must win over a later delivery's copy");
        entry.OriginReference.Should().Be("question:1");
    }

    [Fact]
    public void MergeFrom_WhenAlreadyResolved_DoesNotFlipToTheOtherTerminalState()
    {
        var entry = NewPendingEntry();
        var acceptedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        entry.MarkAccepted(acceptedAt);

        var rejection = NewPendingEntry();
        rejection.MarkRejected("late", new DateTimeOffset(2026, 7, 14, 10, 0, 9, TimeSpan.Zero));

        entry.MergeFrom(rejection);

        entry.ValidationState.Should().Be(
            EvidenceValidationState.Accepted, "once terminal, always terminal — the first resolution wins");
        entry.RejectionReason.Should().BeNull();
        entry.ResolvedAt.Should().Be(acceptedAt);
    }

    private static EvidenceTraceEntry NewPendingEntry()
    {
        return EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            Guid.NewGuid(),
            "question:1",
            DateTimeOffset.UtcNow);
    }
}
