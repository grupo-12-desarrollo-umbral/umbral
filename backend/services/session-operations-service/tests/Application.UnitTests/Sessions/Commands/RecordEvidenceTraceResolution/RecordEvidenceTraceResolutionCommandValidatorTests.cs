using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.RecordEvidenceTraceResolution;

public sealed class RecordEvidenceTraceResolutionCommandValidatorTests
{
    private readonly RecordEvidenceTraceResolutionCommandValidator _validator = new();

    private static readonly DateTimeOffset ResolvedAt =
        new(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);

    [Fact]
    public void Validate_WhenAcceptedAndNoRejectionReason_Passes()
    {
        var command = new RecordEvidenceTraceResolutionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            DateTimeOffset.UtcNow,
            EvidenceValidationState.Accepted,
            RejectionReason: null,
            ResolvedAt);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenRejectedWithReason_Passes()
    {
        var command = new RecordEvidenceTraceResolutionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            DateTimeOffset.UtcNow,
            EvidenceValidationState.Rejected,
            "ScannedValueDoesNotResolveToTarget",
            ResolvedAt);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenPendingState_Fails()
    {
        var command = new RecordEvidenceTraceResolutionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            DateTimeOffset.UtcNow,
            EvidenceValidationState.Pending,
            RejectionReason: null,
            ResolvedAt);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "ResolutionState");
    }

    [Fact]
    public void Validate_WhenRejectedWithoutReason_Fails()
    {
        var command = new RecordEvidenceTraceResolutionCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            DateTimeOffset.UtcNow,
            EvidenceValidationState.Rejected,
            RejectionReason: null,
            ResolvedAt);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RejectionReason");
    }

    [Fact]
    public void Validate_WhenEvidenceSubmissionIdEmpty_Fails()
    {
        var command = new RecordEvidenceTraceResolutionCommand(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            DateTimeOffset.UtcNow,
            EvidenceValidationState.Accepted,
            RejectionReason: null,
            ResolvedAt);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EvidenceSubmissionId");
    }
}
