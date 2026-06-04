using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Commands.DisconnectParticipant;
using umbral_backend.Application.Sessions.Commands.ReconnectAuthenticatedParticipant;
using umbral_backend.Application.Sessions.DTOs;

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
                request.Token),
            cancellationToken);

        _connectionTracker.Add(Context.ConnectionId, result.LiveSessionId, result.SessionParticipantId);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(result.LiveSessionId), cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildTeamGroup(result.TeamId), cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildParticipantGroup(result.SessionParticipantId), cancellationToken);

        return result;
    }

    public async Task JoinLiveSessionAsOperatorAsync(Guid liveSessionId)
    {
        _userContext.Principal = Context.User;
        var cancellationToken = Context.ConnectionAborted;

        EnsureOperatorCaller();
        await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(liveSessionId, cancellationToken);
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(liveSessionId), cancellationToken);
    }

    public async Task LeaveLiveSessionAsync(Guid liveSessionId)
    {
        _userContext.Principal = Context.User;
        var cancellationToken = Context.ConnectionAborted;

        EnsureOperatorCaller();
        await _sessionAdministrationAccessResolver.GetAuthorizedSessionAsync(liveSessionId, cancellationToken);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildLiveSessionGroup(liveSessionId), cancellationToken);
    }

    private void EnsureOperatorCaller()
    {
        if (!string.Equals(_currentUser.Role, "Operator", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException();
        }
    }

    private static string BuildLiveSessionGroup(Guid liveSessionId) => $"live-session:{liveSessionId:D}";

    private static string BuildTeamGroup(Guid teamId) => $"team:{teamId:D}";

    private static string BuildParticipantGroup(Guid sessionParticipantId) => $"participant:{sessionParticipantId:D}";

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionTracker.TryRemove(Context.ConnectionId, out var participant, out var hasRemainingConnections) &&
            !hasRemainingConnections)
        {
            _userContext.Principal = Context.User;
            await _sender.Send(
                new DisconnectParticipantCommand(participant.LiveSessionId, participant.SessionParticipantId),
                CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public sealed record ReconnectParticipantHubRequest(
        Guid TeamId,
        string DisplayName,
        string? Token);
}
