using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Application.Common.Interfaces;

public interface ISessionStateBroadcaster
{
    Task BroadcastStateChangedAsync(
        SessionStateChangedNotificationDto notification,
        CancellationToken cancellationToken);
}
