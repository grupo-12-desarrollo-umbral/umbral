using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="EvidenceSubmissionAcceptedEvent"/> domain fact onto the operator-only
/// SignalR signal (HU-24B). Accepted and Rejected both map to the single *resolved* push, discriminated
/// by ValidationState, because the panel renders one row per submission that flips from pending to its
/// terminal state. Additive to the outbox publisher, which stays (RF-14/RNF-05).
/// </summary>
public sealed class BroadcastEvidenceSubmissionAcceptedNotificationHandler
    : INotificationHandler<EvidenceSubmissionAcceptedEvent>
{
    private readonly IEvidenceSubmissionBroadcaster _broadcaster;

    public BroadcastEvidenceSubmissionAcceptedNotificationHandler(IEvidenceSubmissionBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task Handle(EvidenceSubmissionAcceptedEvent notification, CancellationToken cancellationToken)
    {
        return _broadcaster.BroadcastEvidenceSubmissionResolvedAsync(
            new EvidenceSubmissionResolvedNotificationDto(
                notification.LiveSessionId,
                notification.EvidenceSubmissionId,
                notification.TeamId,
                notification.ActiveSubstageId,
                notification.SubmissionType.ToString(),
                notification.SubmittedAt,
                EvidenceValidationState.Accepted.ToString(),
                RejectionReason: null,
                notification.ResolvedAt),
            cancellationToken);
    }
}
