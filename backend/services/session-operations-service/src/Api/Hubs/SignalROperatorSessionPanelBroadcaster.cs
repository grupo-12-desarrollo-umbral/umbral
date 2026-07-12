using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Api.Hubs;

public sealed class SignalROperatorSessionPanelBroadcaster : IOperatorSessionPanelBroadcaster
{
    public const string OperatorSessionPanelUpdatedMethod = "OperatorSessionPanelUpdated";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SignalROperatorSessionPanelBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastSessionPanelUpdatedAsync(
        OperatorSessionPanelDto panel,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(panel.LiveSessionId))
            .SendCoreAsync(OperatorSessionPanelUpdatedMethod, [panel], cancellationToken);
    }
}
