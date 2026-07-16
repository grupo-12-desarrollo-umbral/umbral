using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="EvidenceSubmissionRejectedEvent"/> domain fact onto the operator-only
/// SignalR signal (HU-24B), sharing the *resolved* push with the accepted handler. RejectionReason is
/// forwarded verbatim: it is already a pre-rendered display message (the QR path supplies
/// TargetResolutionRejectionReason.ToMessage()), not an enum name to switch on.
/// Additive to the outbox publisher, which stays (RF-14/RNF-05).
/// </summary>
public sealed class BroadcastEvidenceSubmissionRejectedNotificationHandler
    : INotificationHandler<EvidenceSubmissionRejectedEvent>
{
    private readonly IEvidenceSubmissionBroadcaster _broadcaster;

    public BroadcastEvidenceSubmissionRejectedNotificationHandler(IEvidenceSubmissionBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task Handle(EvidenceSubmissionRejectedEvent notification, CancellationToken cancellationToken)
    {
        return _broadcaster.BroadcastEvidenceSubmissionResolvedAsync(
            new EvidenceSubmissionResolvedNotificationDto(
                notification.LiveSessionId,
                notification.EvidenceSubmissionId,
                notification.TeamId,
                notification.ActiveSubstageId,
                notification.SubmissionType.ToString(),
                notification.SubmittedAt,
                EvidenceValidationState.Rejected.ToString(),
                notification.RejectionReason,
                notification.ResolvedAt),
            cancellationToken);
    }
}
