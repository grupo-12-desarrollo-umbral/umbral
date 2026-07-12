using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Dtos.Sessions;

namespace umbral_backend.Api.Hubs;

/// <summary>
/// Pushes team-scoped board updates to the <c>team:{teamId}</c> group over the shared
/// <see cref="SessionsHub"/>. Participant connections join this group during
/// <see cref="SessionsHub.ReconnectAsync"/>, so only participants belonging to that team
/// receive the payload. Operators and other teams never see it.
/// </summary>
public sealed class SignalRTeamBoardBroadcaster : ITeamBoardBroadcaster
{
    public const string TeamBoardUpdatedMethod = "TeamBoardUpdated";

    public static string BuildTeamGroup(Guid teamId) => $"team:{teamId:D}";

    private readonly IHubContext<SessionsHub> _hubContext;

    public SignalRTeamBoardBroadcaster(IHubContext<SessionsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BroadcastTeamBoardUpdatedAsync(
        ParticipantTeamBoardDto board,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(BuildTeamGroup(board.TeamId))
            .SendCoreAsync(TeamBoardUpdatedMethod, [board], cancellationToken);
    }
}
