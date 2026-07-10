using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Pushes authoritative session-timer updates to the live-session group over the shared
/// <see cref="SessionsHub"/>.
/// </summary>
public sealed class SignalRSessionTimerBroadcaster : ISessionTimerBroadcaster
{
    public const string TimerUpdatedMethod = "SessionTimerUpdated";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SignalRSessionTimerBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastTimerUpdatedAsync(
        SessionTimerUpdatedNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group($"live-session:{notification.LiveSessionId:D}")
            .SendCoreAsync(TimerUpdatedMethod, [notification], cancellationToken);
    }
}
