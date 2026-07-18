namespace umbral_backend.Application.Common.Interfaces;

public interface IPenaltyBroadcaster
{
    Task PenaltyApplied(
        Guid liveSessionId,
        PenaltyAppliedNotificationDto notification,
        CancellationToken cancellationToken);
}
