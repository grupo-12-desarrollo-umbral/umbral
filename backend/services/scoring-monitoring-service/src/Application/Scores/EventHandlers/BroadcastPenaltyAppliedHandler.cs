using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Scores.EventHandlers;

// Post-commit (the interceptor publishes domain events to MediatR only after SaveChanges), so the toast
// is pushed only once the penalty ledger row is durably persisted — mirroring BroadcastRankingRefreshedHandler.
public sealed class BroadcastPenaltyAppliedHandler : INotificationHandler<PenaltyApplied>
{
    private readonly IPenaltyBroadcaster _penaltyBroadcaster;

    public BroadcastPenaltyAppliedHandler(IPenaltyBroadcaster penaltyBroadcaster)
    {
        _penaltyBroadcaster = penaltyBroadcaster;
    }

    public Task Handle(PenaltyApplied notification, CancellationToken cancellationToken)
    {
        return _penaltyBroadcaster.PenaltyApplied(
            notification.LiveSessionId,
            new PenaltyAppliedNotificationDto(
                notification.LiveSessionId,
                notification.TeamId,
                notification.PenaltyId,
                notification.ScoreEntryId,
                notification.DeductionMagnitude,
                notification.Reason,
                notification.AppliedAt),
            cancellationToken);
    }
}
