using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class TreasureEvidenceSubmissionTests
{
    [Fact]
    public void Accept_RecordsAcceptedQrEvidenceAndBaseContext()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var substageId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 5, TimeSpan.Zero);

        var submission = TreasureEvidenceSubmission.Accept(
            liveSessionId,
            teamId,
            substageId,
            "QR-001",
            targetId,
            participantId,
            submittedAt);

        submission.Should().BeAssignableTo<EvidenceSubmission>();
        submission.LiveSessionId.Should().Be(liveSessionId);
        submission.TeamId.Should().Be(teamId);
        submission.ActiveSubstageId.Should().Be(substageId);
        submission.ScannedValue.Should().Be("QR-001");
        submission.TargetSnapshotId.Should().Be(targetId);
        submission.SubmittedByParticipantId.Should().Be(participantId);
        submission.SubmittedAt.Should().Be(submittedAt);
        submission.SubmissionType.Should().Be(EvidenceSubmissionType.TreasureHuntQrScan);
        submission.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        submission.RejectionReason.Should().BeNull();
        submission.ResolutionRejectionReason.Should().BeNull();
        submission.EvidenceSubmissionId.Should().NotBeEmpty();

        submission.DomainEvents.OfType<EvidenceSubmissionAcceptedEvent>().Should().ContainSingle()
            .Which.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);
    }

    [Fact]
    public void RejectRegisteredTarget_RaisesRejectedEventWithTargetResolutionReason()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var substageId = Guid.NewGuid();
        var submittedAt = new DateTimeOffset(2026, 7, 13, 10, 1, 5, TimeSpan.Zero);

        var submission = TreasureEvidenceSubmission.Begin(
            liveSessionId,
            teamId,
            substageId,
            "WRONG-QR",
            targetSnapshotId: null,
            Guid.NewGuid(),
            submittedAt);

        submission.RejectRegisteredTarget(
            TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget,
            submittedAt);

        submission.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        submission.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget);
        submission.RejectionReason.Should().BeNull();

        var rejectedEvent = submission.DomainEvents.OfType<EvidenceSubmissionRejectedEvent>().Should().ContainSingle().Which;
        rejectedEvent.EvidenceSubmissionId.Should().Be(submission.EvidenceSubmissionId);
        rejectedEvent.RejectionReason.Should().Be("The scanned value does not resolve to a target.");
        rejectedEvent.ResolvedAt.Should().Be(submittedAt);
    }

    [Theory]
    [InlineData(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget, 1, "The scanned value does not resolve to a target.")]
    [InlineData(TargetResolutionRejectionReason.TargetOutsideActiveSubstage, 2, "The resolved target does not belong to the active treasure-hunt substage.")]
    [InlineData(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam, 3, "The target has already been resolved by this team.")]
    [InlineData(TargetResolutionRejectionReason.ScannedValueResolvesToMultipleTargets, 4, "The scanned value matches more than one target in this mission and cannot be resolved.")]
    public void TargetResolutionRejectionReason_HasStableValueAndMessage(
        TargetResolutionRejectionReason reason,
        int expectedValue,
        string expectedMessage)
    {
        ((int)reason).Should().Be(expectedValue);
        reason.ToMessage().Should().Be(expectedMessage);
    }

    [Fact]
    public void Accept_WithUnsetSubmittedAt_IsRejectedByTheSharedBase()
    {
        var act = () => TreasureEvidenceSubmission.Accept(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "QR-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            default);

        act.Should().Throw<EvidenceSubmissionTimestampRequiredException>();
    }

    [Fact]
    public void Begin_WithUnsetSubmittedAt_IsRejectedByTheSharedBase()
    {
        var act = () => TreasureEvidenceSubmission.Begin(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WRONG-QR",
            targetSnapshotId: null,
            Guid.NewGuid(),
            default);

        act.Should().Throw<EvidenceSubmissionTimestampRequiredException>();
    }

    [Fact]
    public void TargetResolutionRejectionReason_WhenUnknown_RejectsMessageMapping()
    {
        var act = () => ((TargetResolutionRejectionReason)99).ToMessage();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
