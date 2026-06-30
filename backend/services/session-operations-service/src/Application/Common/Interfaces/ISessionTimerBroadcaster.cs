using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionTimerBroadcaster
{
    Task BroadcastTimerUpdatedAsync(
        SessionTimerUpdatedNotificationDto notification,
        CancellationToken cancellationToken);
}
