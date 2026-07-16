using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Application.Sessions.EventHandlers;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.UnitTests.Sessions.EventHandlers;

// The operator-only SignalR bridge for evidence/submission activity (HU-24B). The three domain facts
// map to two pushes — registration, and a shared resolved push discriminated by ValidationState — whose
// fields must line up with the REST trace row the panel merges them onto.
public sealed class BroadcastEvidenceSubmissionNotificationHandlerTests
{
    private static readonly DateTimeOffset SubmittedAt = new(2026, 7, 16, 10, 1, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset ResolvedAt = new(2026, 7, 16, 10, 1, 9, TimeSpan.Zero);

    [Fact]
    public async Task RegisteredHandler_BroadcastsSubmissionWithOriginAndPendingState()
    {
        var (broadcaster, registered, _) = CreateBroadcaster();
        var handler = new BroadcastEvidenceSubmissionRegisteredNotificationHandler(broadcaster.Object);

        var domainEvent = new EvidenceSubmissionRegisteredEvent(
            liveSessionId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            evidenceSubmissionId: Guid.NewGuid(),
            activeSubstageId: Guid.NewGuid(),
            submissionType: EvidenceSubmissionType.TreasureHuntQrScan,
            submittedAt: SubmittedAt,
            validationState: EvidenceValidationState.Pending,
            originReference: "target:9f1c");

        await handler.Handle(domainEvent, CancellationToken.None);

        registered.Value.Should().NotBeNull();
        var captured = registered.Value!;
        captured.LiveSessionId.Should().Be(domainEvent.LiveSessionId);
        captured.EvidenceSubmissionId.Should().Be(domainEvent.EvidenceSubmissionId);
        captured.TeamId.Should().Be(domainEvent.TeamId);
        captured.ActiveSubstageId.Should().Be(domainEvent.ActiveSubstageId);
        captured.SubmissionType.Should().Be("TreasureHuntQrScan");
        captured.OriginReference.Should().Be("target:9f1c");
        captured.SubmittedAt.Should().Be(SubmittedAt);
        captured.ValidationState.Should().Be("Pending");
    }

    [Fact]
    public async Task AcceptedHandler_BroadcastsResolvedAsAcceptedWithNoRejectionReason()
    {
        var (broadcaster, _, resolved) = CreateBroadcaster();
        var handler = new BroadcastEvidenceSubmissionAcceptedNotificationHandler(broadcaster.Object);

        var domainEvent = new EvidenceSubmissionAcceptedEvent(
            liveSessionId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            evidenceSubmissionId: Guid.NewGuid(),
            activeSubstageId: Guid.NewGuid(),
            submissionType: EvidenceSubmissionType.TreasureHuntQrScan,
            submittedAt: SubmittedAt,
            resolvedAt: ResolvedAt);

        await handler.Handle(domainEvent, CancellationToken.None);

        resolved.Value.Should().NotBeNull();
        var captured = resolved.Value!;
        captured.EvidenceSubmissionId.Should().Be(domainEvent.EvidenceSubmissionId);
        captured.ValidationState.Should().Be("Accepted");
        captured.RejectionReason.Should().BeNull();
        captured.SubmittedAt.Should().Be(SubmittedAt);
        captured.ResolvedAt.Should().Be(ResolvedAt);
    }

    [Fact]
    public async Task RejectedHandler_ForwardsRejectionReasonVerbatimAsDisplayCopy()
    {
        var (broadcaster, _, resolved) = CreateBroadcaster();
        var handler = new BroadcastEvidenceSubmissionRejectedNotificationHandler(broadcaster.Object);

        // The QR path supplies TargetResolutionRejectionReason.ToMessage() — human copy, not an enum
        // name. It must reach the panel unmapped.
        const string Reason = "Este objetivo ya fue resuelto por otro equipo.";
        var domainEvent = new EvidenceSubmissionRejectedEvent(
            liveSessionId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            evidenceSubmissionId: Guid.NewGuid(),
            activeSubstageId: Guid.NewGuid(),
            submissionType: EvidenceSubmissionType.TreasureHuntQrScan,
            submittedAt: SubmittedAt,
            rejectionReason: Reason,
            resolvedAt: ResolvedAt);

        await handler.Handle(domainEvent, CancellationToken.None);

        resolved.Value.Should().NotBeNull();
        var captured = resolved.Value!;
        captured.ValidationState.Should().Be("Rejected");
        captured.RejectionReason.Should().Be(Reason);
        captured.ResolvedAt.Should().Be(ResolvedAt);
    }

    [Fact]
    public async Task RegisteredHandler_BroadcastsTriviaAnswersToo_TypedBySubmissionType()
    {
        // A trivia submission raises these evidence facts as well as AnswerRegisteredEvent, so the panel
        // legitimately shows both forms. SubmissionType is what lets it label them apart.
        var (broadcaster, registered, _) = CreateBroadcaster();
        var handler = new BroadcastEvidenceSubmissionRegisteredNotificationHandler(broadcaster.Object);

        await handler.Handle(
            new EvidenceSubmissionRegisteredEvent(
                liveSessionId: Guid.NewGuid(),
                teamId: Guid.NewGuid(),
                evidenceSubmissionId: Guid.NewGuid(),
                activeSubstageId: Guid.NewGuid(),
                submissionType: EvidenceSubmissionType.TriviaAnswer,
                submittedAt: SubmittedAt,
                validationState: EvidenceValidationState.Pending,
                // TriviaAnswerSubmission.DescribeOrigin() — the trivia form's origin grain.
                originReference: "question:3"),
            CancellationToken.None);

        registered.Value!.SubmissionType.Should().Be("TriviaAnswer");
        registered.Value!.OriginReference.Should().Be("question:3");
    }

    // The push and the REST snapshot describe the same row and the panel merges them by
    // EvidenceSubmissionId. If the trace row grew a field the push never learned about, the merged row
    // would flicker between snapshot and push values — so pin the shapes together.
    [Fact]
    public void NotificationDtos_CoverEveryEvidenceTraceItemField()
    {
        var traceFields = typeof(EvidenceTraceItemDto).GetProperties().Select(p => p.Name);
        var pushFields = typeof(EvidenceSubmissionRegisteredNotificationDto).GetProperties()
            .Concat(typeof(EvidenceSubmissionResolvedNotificationDto).GetProperties())
            .Select(p => p.Name)
            .ToArray();

        pushFields.Should().Contain(traceFields);
    }

    // Neither resolution event carries OriginReference, so the resolved push must not claim to.
    [Fact]
    public void ResolvedDto_HasNoOriginReference()
    {
        typeof(EvidenceSubmissionResolvedNotificationDto)
            .GetProperties()
            .Select(p => p.Name)
            .Should().NotContain("OriginReference");
    }

    private static (Mock<IEvidenceSubmissionBroadcaster> Broadcaster,
        Captured<EvidenceSubmissionRegisteredNotificationDto> Registered,
        Captured<EvidenceSubmissionResolvedNotificationDto> Resolved) CreateBroadcaster()
    {
        var registered = new Captured<EvidenceSubmissionRegisteredNotificationDto>();
        var resolved = new Captured<EvidenceSubmissionResolvedNotificationDto>();
        var broadcaster = new Mock<IEvidenceSubmissionBroadcaster>();
        broadcaster
            .Setup(b => b.BroadcastEvidenceSubmissionRegisteredAsync(
                It.IsAny<EvidenceSubmissionRegisteredNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<EvidenceSubmissionRegisteredNotificationDto, CancellationToken>((dto, _) => registered.Value = dto)
            .Returns(Task.CompletedTask);
        broadcaster
            .Setup(b => b.BroadcastEvidenceSubmissionResolvedAsync(
                It.IsAny<EvidenceSubmissionResolvedNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<EvidenceSubmissionResolvedNotificationDto, CancellationToken>((dto, _) => resolved.Value = dto)
            .Returns(Task.CompletedTask);
        return (broadcaster, registered, resolved);
    }

    private sealed class Captured<T>
        where T : class
    {
        public T? Value { get; set; }
    }
}
