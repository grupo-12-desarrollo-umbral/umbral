using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Pushes session-state changes to the live-session group over the shared <see cref="SessionsHub"/>.
/// </summary>
public sealed class SessionStateBroadcaster : ISessionStateBroadcaster
{
    public const string StateChangedMethod = "SessionStateChanged";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SessionStateBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastStateChangedAsync(
        SessionStateChangedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendAsync(StateChangedMethod, notification, cancellationToken);
    }
}
