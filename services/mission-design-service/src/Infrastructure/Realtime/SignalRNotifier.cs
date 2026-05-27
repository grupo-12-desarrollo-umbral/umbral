using umbral_backend.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace umbral_backend.Infrastructure.Realtime;

public class SignalRNotifier : INotifier
{
    private readonly IHubContext<Hub> _hubContext;

    public SignalRNotifier(IHubContext<Hub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAsync(string userId, string message, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.User(userId).SendAsync("Notification", message, cancellationToken);
    }
}
