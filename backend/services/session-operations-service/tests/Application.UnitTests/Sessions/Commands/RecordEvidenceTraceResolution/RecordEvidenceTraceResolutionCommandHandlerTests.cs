using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceResolution;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.RecordEvidenceTraceResolution;

public sealed class RecordEvidenceTraceResolutionCommandHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResolvedAt =
        new(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenEntryExistsAndPending_AppliesAccepted()
    {
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            Guid.NewGuid(),
            "question:1",
            SubmittedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceResolutionCommandHandler(repo.Object);

        await handler.Handle(
            new RecordEvidenceTraceResolutionCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                existing.SubmissionType,
                SubmittedAt,
                EvidenceValidationState.Accepted,
                RejectionReason: null,
                ResolvedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.ValidationState == EvidenceValidationState.Accepted &&
                entry.ResolvedAt == ResolvedAt),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryExistsAndPending_AppliesRejected()
    {
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            Guid.NewGuid(),
            "target:abc",
            SubmittedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceResolutionCommandHandler(repo.Object);

        await handler.Handle(
            new RecordEvidenceTraceResolutionCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                existing.SubmissionType,
                SubmittedAt,
                EvidenceValidationState.Rejected,
                "ScannedValueDoesNotResolveToTarget",
                ResolvedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.ValidationState == EvidenceValidationState.Rejected &&
                entry.RejectionReason == "ScannedValueDoesNotResolveToTarget" &&
                entry.ResolvedAt == ResolvedAt),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryDoesNotExist_CreatesAndAppliesResolution()
    {
        var submissionId = Guid.NewGuid();
        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvidenceTraceEntry?)null);
        var handler = new RecordEvidenceTraceResolutionCommandHandler(repo.Object);

        await handler.Handle(
            new RecordEvidenceTraceResolutionCommand(
                submissionId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                EvidenceSubmissionType.TriviaAnswer,
                SubmittedAt,
                EvidenceValidationState.Accepted,
                RejectionReason: null,
                ResolvedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.EvidenceSubmissionId == submissionId &&
                entry.ValidationState == EvidenceValidationState.Accepted &&
                entry.ResolvedAt == ResolvedAt),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryAlreadyAccepted_IsNoOpOrderTolerant()
    {
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            Guid.NewGuid(),
            "question:1",
            SubmittedAt);
        existing.MarkAccepted(ResolvedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceResolutionCommandHandler(repo.Object);

        // Try to reject an already-accepted entry — order-tolerant: no-op.
        await handler.Handle(
            new RecordEvidenceTraceResolutionCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                existing.SubmissionType,
                SubmittedAt,
                EvidenceValidationState.Rejected,
                "SomeReason",
                ResolvedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.IsAny<EvidenceTraceEntry>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenEntryAlreadyRejected_IsNoOpOrderTolerant()
    {
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            Guid.NewGuid(),
            "target:abc",
            SubmittedAt);
        existing.MarkRejected("ScannedValueDoesNotResolveToTarget", ResolvedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceResolutionCommandHandler(repo.Object);

        // Try to accept an already-rejected entry — order-tolerant: no-op.
        await handler.Handle(
            new RecordEvidenceTraceResolutionCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                existing.SubmissionType,
                SubmittedAt,
                EvidenceValidationState.Accepted,
                RejectionReason: null,
                ResolvedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.IsAny<EvidenceTraceEntry>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReapplyingSameResolution_IsIdempotent()
    {
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            Guid.NewGuid(),
            "question:1",
            SubmittedAt);
        existing.MarkAccepted(ResolvedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceResolutionCommandHandler(repo.Object);

        // Already Accepted — order-tolerant guard fires before entity idempotency.
        await handler.Handle(
            new RecordEvidenceTraceResolutionCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                existing.SubmissionType,
                SubmittedAt,
                EvidenceValidationState.Accepted,
                RejectionReason: null,
                new DateTimeOffset(2026, 7, 14, 11, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.IsAny<EvidenceTraceEntry>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
