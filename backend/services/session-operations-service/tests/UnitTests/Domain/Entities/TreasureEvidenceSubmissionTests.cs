using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

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
    }

    [Theory]
    [InlineData(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget, 1, "The scanned value does not resolve to a target.")]
    [InlineData(TargetResolutionRejectionReason.TargetOutsideActiveSubstage, 2, "The resolved target does not belong to the active treasure-hunt substage.")]
    [InlineData(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam, 3, "The target has already been resolved by this team.")]
    public void TargetResolutionRejectionReason_HasStableValueAndMessage(
        TargetResolutionRejectionReason reason,
        int expectedValue,
        string expectedMessage)
    {
        ((int)reason).Should().Be(expectedValue);
        reason.ToMessage().Should().Be(expectedMessage);
    }

    [Fact]
    public void TargetResolutionRejectionReason_WhenUnknown_RejectsMessageMapping()
    {
        var act = () => ((TargetResolutionRejectionReason)99).ToMessage();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
