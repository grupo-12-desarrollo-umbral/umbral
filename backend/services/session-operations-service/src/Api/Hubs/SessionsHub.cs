using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.Common;

namespace umbral_backend.Api.Hubs;

[Authorize(Policy = AuthorizationPolicies.ParticipantOrOperator)]
public sealed class SessionsHub : Hub
{
    private readonly ISender _sender;
    private readonly CurrentUserContext _userContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISessionAdministrationAccessResolver _sessionAdministrationAccessResolver;
    private readonly ConnectionTracker _connectionTracker;

    public SessionsHub(
        ISender sender,
        CurrentUserContext userContext,
        ICurrentUser currentUser,
        ISessionAdministrationAccessResolver sessionAdministrationAccessResolver,
        ConnectionTracker connectionTracker)
    {
        _sender = sender;
        _userContext = userContext;
        _currentUser = currentUser;
        _sessionAdministrationAccessResolver = sessionAdministrationAccessResolver;
        _connectionTracker = connectionTracker;
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
                request.Token,
                Context.ConnectionId),
            cancellationToken);

        _connectionTracker.Add(Context.ConnectionId, result.LiveSessionId, result.SessionParticipantId);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(result.LiveSessionId), cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildTeamGroup(result.TeamId), cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildParticipantGroup(result.SessionParticipantId), cancellationToken);

        return result;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _userContext.Principal = Context.User;

        // The in-memory tracker is only a ConnectionId -> (session, participant) lookup now, not the
        // disconnect authority: the aggregate owns the connection leases and makes the decrement
        // decision (idempotent + guarded on the last connection). So we always dispatch the
        // connection-keyed disconnect and let the aggregate decide, rather than gating on a
        // process-local count that a connection landing on another replica would not see.
        if (_connectionTracker.TryRemove(Context.ConnectionId, out var participant, out _))
        {
            await _sender.Send(
                new DisconnectParticipantCommand(
                    participant.LiveSessionId,
                    participant.SessionParticipantId,
                    Context.ConnectionId),
                CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinLiveSessionAsOperatorAsync(Guid liveSessionId)
    {
        _userContext.Principal = Context.User;
        var cancellationToken = Context.ConnectionAborted;

        EnsureOperatorCaller();
        await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(liveSessionId, cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(liveSessionId), cancellationToken);
        // HU-34: only operators join the operator-only group that receives the "team answered" signal.
        // The EnsureOperatorCaller gate above is what keeps participant connections out of this group.
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildOperatorGroup(liveSessionId), cancellationToken);
    }

    public async Task LeaveLiveSessionAsync(Guid liveSessionId)
    {
        _userContext.Principal = Context.User;
        var cancellationToken = Context.ConnectionAborted;

        EnsureOperatorCaller();
        await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(liveSessionId, cancellationToken);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(liveSessionId), cancellationToken);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildOperatorGroup(liveSessionId), cancellationToken);
    }

    private void EnsureOperatorCaller()
    {
        if (!string.Equals(_currentUser.Role, "Operator", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }
    }

    private static string BuildLiveSessionGroup(Guid liveSessionId) => $"live-session:{liveSessionId:D}";

    // Delegate to the broadcaster so hub membership and the "team answered" broadcast never drift on
    // the operator-only group name (HU-34).
    private static string BuildOperatorGroup(Guid liveSessionId) =>
        SignalRTeamAnsweredBroadcaster.BuildOperatorGroup(liveSessionId);

    private static string BuildTeamGroup(Guid teamId) => $"team:{teamId:D}";

    private static string BuildParticipantGroup(Guid sessionParticipantId) => $"participant:{sessionParticipantId:D}";

    public sealed record ReconnectParticipantHubRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);
}
