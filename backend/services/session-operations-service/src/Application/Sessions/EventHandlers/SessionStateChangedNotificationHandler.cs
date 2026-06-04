using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.DTOs;
using umbral_backend.Domain.Events;

namespace umbral_backend.Application.Sessions.EventHandlers;

/// <summary>
/// Bridges the <see cref="SessionStateChangedEvent"/> domain event onto the SignalR broadcast,
/// keeping the transport detail out of the application core.
/// </summary>
public sealed class SessionStateChangedNotificationHandler : INotificationHandler<SessionStateChangedEvent>
{
    private readonly ISessionStateBroadcaster _broadcaster;

    public SessionStateChangedNotificationHandler(ISessionStateBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public Task Handle(SessionStateChangedEvent notification, CancellationToken cancellationToken)
    {
        return _broadcaster.BroadcastStateChangedAsync(
            new SessionStateChangedNotificationDto(
                notification.LiveSessionId,
                notification.PreviousState.ToString(),
                notification.CurrentState.ToString(),
                notification.ChangedAt),
            cancellationToken);
    }
}
