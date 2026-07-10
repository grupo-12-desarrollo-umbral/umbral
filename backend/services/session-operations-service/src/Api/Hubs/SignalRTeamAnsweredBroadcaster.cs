using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common.Notifications;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Operator-only transport for the HU-34 "team answered" indicator. Reuses the shared
/// <see cref="SessionsHub"/> (injected as <see cref="IHubContext{SessionsHub}"/>) but targets a
/// DISTINCT operator-only group — <c>live-session-operators:{id}</c> — never the participant-shared
/// <c>live-session:{id}</c> group. Participant connections are only ever added to
/// <c>live-session:{id}</c>, <c>team:{id}</c>, and <c>participant:{id}</c>, so they can never receive
/// this signal. The payload (<see cref="TeamAnsweredNotificationDto"/>) already carries no
/// correctness/points; this group boundary is the remaining privacy gate.
/// </summary>
public sealed class SignalRTeamAnsweredBroadcaster : ITeamAnsweredBroadcaster
{
    public const string TeamAnsweredMethod = "TeamAnswered";

    // Operator-only group — deliberately different from the participant-shared "live-session:{id}".
    public static string BuildOperatorGroup(Guid liveSessionId) => $"live-session-operators:{liveSessionId:D}";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SignalRTeamAnsweredBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastTeamAnsweredAsync(
        TeamAnsweredNotificationDto notification,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(BuildOperatorGroup(notification.LiveSessionId))
            .SendCoreAsync(TeamAnsweredMethod, [notification], cancellationToken);
    }
}
