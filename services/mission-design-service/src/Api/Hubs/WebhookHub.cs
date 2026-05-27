using Microsoft.AspNetCore.SignalR;

namespace umbral_backend.Web.Hubs;

public class WebhookHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Webhooks");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "Webhooks");
        await base.OnDisconnectedAsync(exception);
    }
}
