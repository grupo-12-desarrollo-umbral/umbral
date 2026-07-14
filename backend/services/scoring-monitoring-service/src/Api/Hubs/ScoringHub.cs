using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Services;

namespace umbral_backend.Api.Hubs;

[Authorize(Policy = AuthorizationPolicies.ParticipantOrOperator)]
public sealed class ScoringHub : Hub
{
    public async Task JoinSessionGroup(Guid liveSessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildSessionGroup(liveSessionId), Context.ConnectionAborted);
    }

    public async Task LeaveSessionGroup(Guid liveSessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildSessionGroup(liveSessionId), Context.ConnectionAborted);
    }

    internal static string BuildSessionGroup(Guid liveSessionId) => $"live-session:{liveSessionId:D}";
}
