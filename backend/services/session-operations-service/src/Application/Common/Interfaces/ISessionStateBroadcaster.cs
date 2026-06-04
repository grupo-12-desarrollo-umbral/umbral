using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionStateBroadcaster
{
    Task BroadcastStateChangedAsync(
        SessionStateChangedNotificationDto notification,
        CancellationToken cancellationToken);
}
