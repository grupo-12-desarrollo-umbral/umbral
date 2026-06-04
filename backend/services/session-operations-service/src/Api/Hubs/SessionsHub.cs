using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;
using umbral_backend.Api.Services;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.DTOs;

namespace umbral_backend.Api.Hubs;

[Authorize(Policy = AuthorizationPolicies.Participant)]
public sealed class SessionsHub : Hub
{
    private readonly ISender _sender;
    private readonly ConnectionTracker _connectionTracker;
    private readonly CurrentUserContext _userContext;

    public SessionsHub(
        ISender sender,
        ConnectionTracker connectionTracker,
        CurrentUserContext userContext)
    {
        _sender = sender;
        _connectionTracker = connectionTracker;
        _userContext = userContext;
    }

    public async Task<ReconnectParticipantResultDto> ReconnectAsync(
        Guid liveSessionId,
        ReconnectParticipantHubRequest request)
    {
        _userContext.Principal = Context.User;
        var cancellationToken = Context.ConnectionAborted;

        var result = await _sender.Send(
            new ReconnectAuthenticatedParticipantCommand(
                liveSessionId,
                request.TeamId,
                request.DisplayName,
                request.Token),
            cancellationToken);

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(result.LiveSessionId), cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildTeamGroup(result.TeamId), cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildParticipantGroup(result.SessionParticipantId), cancellationToken);
        _connectionTracker.Add(Context.ConnectionId, result.LiveSessionId, result.SessionParticipantId);

        return result;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (_connectionTracker.TryRemove(Context.ConnectionId, out var participant, out var hasRemainingConnections) &&
                !hasRemainingConnections)
            {
                _userContext.Principal = Context.User;

                await _sender.Send(new DisconnectParticipantCommand(
                    participant.LiveSessionId,
                    participant.SessionParticipantId));
            }
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    private static string BuildLiveSessionGroup(Guid liveSessionId) => $"live-session:{liveSessionId:D}";

    private static string BuildTeamGroup(Guid teamId) => $"team:{teamId:D}";

    private static string BuildParticipantGroup(Guid sessionParticipantId) => $"participant:{sessionParticipantId:D}";

    public sealed record ReconnectParticipantHubRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);
}
