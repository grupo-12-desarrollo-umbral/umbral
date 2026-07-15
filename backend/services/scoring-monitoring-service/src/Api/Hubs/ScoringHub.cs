using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;

namespace umbral_backend.Api.Hubs;

[Authorize(Policy = AuthorizationPolicies.ParticipantOrOperator)]
public sealed class ScoringHub : Hub
{
    private readonly IRankingSessionMembershipGuard _membershipGuard;
    private readonly CurrentUserContext _currentUserContext;

    public ScoringHub(IRankingSessionMembershipGuard membershipGuard, CurrentUserContext currentUserContext)
    {
        _membershipGuard = membershipGuard;
        _currentUserContext = currentUserContext;
    }

    // Joining a session group hands the caller every RankingChanged push for that session, so it must
    // carry the same session-scoped membership check as the REST ranking endpoint — the hub-level
    // [Authorize] only proves "some participant/operator", not "a member of THIS session's team".
    // Without this, any authenticated participant could join an arbitrary session's group and receive
    // its full standings. teamId identifies the caller's team for that check (ReferenceTeamId).
    public async Task JoinSessionGroup(Guid liveSessionId, Guid teamId)
    {
        // A hub method invocation has no active HttpContext, so IHttpContextAccessor-backed ICurrentUser
        // (which the guard's downstream membership call reads for its trusted headers) would resolve
        // empty and deny even legitimate members. Seed the request-scoped principal fallback from the
        // authenticated connection so identity flows to the guard. Scoped => same instance ICurrentUser sees.
        _currentUserContext.Principal = Context.User;

        // Throws ForbiddenAccessException (surfaced to the client as a HubException) when the caller is
        // not an active member of teamId in this session; the client degrades to REST-only ranking.
        await _membershipGuard.EnsureAllowedAsync(liveSessionId, teamId, Context.ConnectionAborted);

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildSessionGroup(liveSessionId), Context.ConnectionAborted);
    }

    public async Task LeaveSessionGroup(Guid liveSessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildSessionGroup(liveSessionId), Context.ConnectionAborted);
    }

    internal static string BuildSessionGroup(Guid liveSessionId) => $"live-session:{liveSessionId:D}";
}
