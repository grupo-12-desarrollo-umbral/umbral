using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionTimerBroadcaster
{
    Task BroadcastTimerUpdatedAsync(
        SessionTimerUpdatedNotificationDto notification,
        CancellationToken cancellationToken);
}
