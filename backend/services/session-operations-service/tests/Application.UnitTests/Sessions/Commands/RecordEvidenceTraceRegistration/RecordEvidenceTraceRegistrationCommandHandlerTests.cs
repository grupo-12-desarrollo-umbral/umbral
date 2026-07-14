using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.RecordEvidenceTraceRegistration;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.Application.UnitTests.Sessions.Commands.RecordEvidenceTraceRegistration;

public sealed class RecordEvidenceTraceRegistrationCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenEntryDoesNotExist_CreatesEntryWithPendingState()
    {
        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvidenceTraceEntry?)null);
        var handler = new RecordEvidenceTraceRegistrationCommandHandler(repo.Object);
        var submittedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);

        await handler.Handle(
            new RecordEvidenceTraceRegistrationCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                EvidenceSubmissionType.TriviaAnswer,
                Guid.NewGuid(),
                "question:1",
                submittedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.ValidationState == EvidenceValidationState.Pending &&
                entry.OriginReference == "question:1" &&
                entry.SubmittedAt == submittedAt),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryExistsAndResolutionApplied_FillsContextPreservingState()
    {
        // Simulate a resolution arrived first (entry exists with minimal context but already accepted)
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            submittedByParticipantId: null,
            originReference: null,
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero));
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        existing.MarkAccepted(resolvedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceRegistrationCommandHandler(repo.Object);

        await handler.Handle(
            new RecordEvidenceTraceRegistrationCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                EvidenceSubmissionType.TreasureHuntQrScan,
                Guid.NewGuid(),
                "target:abc",
                existing.SubmittedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.ValidationState == EvidenceValidationState.Accepted &&
                entry.OriginReference == "target:abc" &&
                entry.ResolvedAt == resolvedAt),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEntryExistsAndRejected_ReappliesRejectionPreservingContext()
    {
        var existing = EvidenceTraceEntry.ForRegistration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TreasureHuntQrScan,
            submittedByParticipantId: null,
            originReference: null,
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero));
        var resolvedAt = new DateTimeOffset(2026, 7, 14, 10, 0, 5, TimeSpan.Zero);
        existing.MarkRejected("ScannedValueDoesNotResolveToTarget", resolvedAt);

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(
                existing.EvidenceSubmissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceRegistrationCommandHandler(repo.Object);

        await handler.Handle(
            new RecordEvidenceTraceRegistrationCommand(
                existing.EvidenceSubmissionId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                EvidenceSubmissionType.TreasureHuntQrScan,
                Guid.NewGuid(),
                "target:abc",
                existing.SubmittedAt),
            CancellationToken.None);

        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.ValidationState == EvidenceValidationState.Rejected &&
                entry.RejectionReason == "ScannedValueDoesNotResolveToTarget" &&
                entry.OriginReference == "target:abc"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReapplyingRegistration_IsIdempotent()
    {
        var entryId = Guid.NewGuid();
        var existing = EvidenceTraceEntry.ForRegistration(
            entryId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            EvidenceSubmissionType.TriviaAnswer,
            Guid.NewGuid(),
            "question:1",
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero));

        var repo = new Mock<IEvidenceTraceRepository>();
        repo.Setup(r => r.GetByEvidenceSubmissionIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new RecordEvidenceTraceRegistrationCommandHandler(repo.Object);

        await handler.Handle(
            new RecordEvidenceTraceRegistrationCommand(
                entryId,
                existing.LiveSessionId,
                existing.TeamId,
                existing.ActiveSubstageId,
                existing.SubmissionType,
                existing.SubmittedByParticipantId,
                existing.OriginReference,
                existing.SubmittedAt),
            CancellationToken.None);

        // The entry is overwritten (upsert), still Pending, same context.
        repo.Verify(r => r.UpsertAsync(
            It.Is<EvidenceTraceEntry>(entry =>
                entry.EvidenceSubmissionId == entryId &&
                entry.ValidationState == EvidenceValidationState.Pending),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
