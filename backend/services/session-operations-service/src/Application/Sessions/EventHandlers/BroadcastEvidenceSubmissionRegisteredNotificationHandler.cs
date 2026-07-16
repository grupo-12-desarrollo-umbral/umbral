using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="EvidenceSubmissionRegisteredEvent"/> domain fact onto the operator-only
/// SignalR signal (HU-24B). Additive to
/// <see cref="PublishEvidenceSubmissionRegisteredIntegrationEventHandler"/>, which stays: that one runs
/// pre-commit on the outbox path and feeds the REST trace via RabbitMQ (RF-14/RNF-05); this one runs
/// post-commit on the MediatR fan-out and is what makes the panel live. Because the RabbitMQ leg is
/// asynchronous, this push routinely lands BEFORE the REST row exists — the panel merges by
/// EvidenceSubmissionId rather than assuming snapshot-then-deltas.
/// </summary>
public sealed class BroadcastEvidenceSubmissionRegisteredNotificationHandler
    : INotificationHandler<EvidenceSubmissionRegisteredEvent>
{
    private readonly IEvidenceSubmissionBroadcaster _broadcaster;

    public BroadcastEvidenceSubmissionRegisteredNotificationHandler(IEvidenceSubmissionBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task Handle(EvidenceSubmissionRegisteredEvent notification, CancellationToken cancellationToken)
    {
        return _broadcaster.BroadcastEvidenceSubmissionRegisteredAsync(
            new EvidenceSubmissionRegisteredNotificationDto(
                notification.LiveSessionId,
                notification.EvidenceSubmissionId,
                notification.TeamId,
                notification.ActiveSubstageId,
                notification.SubmissionType.ToString(),
                notification.OriginReference,
                notification.SubmittedAt,
                notification.ValidationState.ToString()),
            cancellationToken);
    }
}
