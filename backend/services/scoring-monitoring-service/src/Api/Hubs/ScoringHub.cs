using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common.Authorization;

namespace umbral_backend.Api.Hubs;

[Authorize(Policy = AuthorizationPolicies.ParticipantOrOperator)]
public sealed class ScoringHub : Hub
{
    private readonly IRankingSessionMembershipGuard _membershipGuard;
    private readonly IScoringSessionAccessResolver _accessResolver;
    private readonly CurrentUserContext _currentUserContext;

    public ScoringHub(
        IRankingSessionMembershipGuard membershipGuard,
        IScoringSessionAccessResolver accessResolver,
        CurrentUserContext currentUserContext)
    {
        _membershipGuard = membershipGuard;
        _accessResolver = accessResolver;
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

    // Operator counterpart of JoinSessionGroup. An operator is a member of no team, so the membership
    // guard above can never pass for them — access is proven instead by session assignment, via the same
    // resolver the penalty flow uses (Administrator, or the Operator assigned to THIS session). Lands the
    // connection in the SAME live-session:{id} group: the standings are session-wide, not audience-shaped.
    public async Task JoinSessionGroupAsOperatorAsync(Guid liveSessionId)
    {
        // A hub invocation has no HttpContext, so seed the request-scoped principal fallback the same way
        // JoinSessionGroup does — the resolver's ICurrentUser would otherwise resolve empty and deny.
        _currentUserContext.Principal = Context.User;

        // Throws ForbiddenAccessException (surfaced as a HubException) when the caller is not an assigned
        // operator for this session; the client degrades to the REST snapshot.
        await _accessResolver.EnsureAccessAsync(liveSessionId, Context.ConnectionAborted);

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildSessionGroup(liveSessionId), Context.ConnectionAborted);
    }

    public async Task LeaveSessionGroup(Guid liveSessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildSessionGroup(liveSessionId), Context.ConnectionAborted);
    }

    internal static string BuildSessionGroup(Guid liveSessionId) => $"live-session:{liveSessionId:D}";
}
