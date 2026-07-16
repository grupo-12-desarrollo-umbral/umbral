using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using umbral_backend.Api.Hubs;
using umbral_backend.Api.Services;
using umbral_backend.Application.Common.Exceptions;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Scores.Common.Authorization;

namespace umbral_backend.ScoringMonitoring.Api.UnitTests.Hubs;

public sealed class ScoringHubTests
{
    private sealed record HubHarness(
        ScoringHub Hub,
        Mock<IGroupManager> Groups,
        Mock<IRankingSessionMembershipGuard> Guard,
        Mock<IScoringSessionAccessResolver> AccessResolver,
        CurrentUserContext UserContext);

    private static HubHarness BuildHub(ClaimsPrincipal? user = null)
    {
        var groups = new Mock<IGroupManager>();
        var guard = new Mock<IRankingSessionMembershipGuard>();
        var accessResolver = new Mock<IScoringSessionAccessResolver>();
        var userContext = new CurrentUserContext();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("connection-1");
        context.SetupGet(c => c.ConnectionAborted).Returns(CancellationToken.None);
        context.SetupGet(c => c.User).Returns(user);

        var hub = new ScoringHub(guard.Object, accessResolver.Object, userContext)
        {
            Context = context.Object,
            Groups = groups.Object
        };

        return new HubHarness(hub, groups, guard, accessResolver, userContext);
    }

    [Fact]
    public async Task JoinSessionGroup_AllowedMember_AddsCallerConnectionToTheLiveSessionGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var (hub, groups, guard, _, _) = BuildHub();

        await hub.JoinSessionGroup(liveSessionId, teamId);

        guard.Verify(
            g => g.EnsureAllowedAsync(liveSessionId, teamId, CancellationToken.None),
            Times.Once);
        groups.Verify(
            g => g.AddToGroupAsync("connection-1", $"live-session:{liveSessionId:D}", CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task JoinSessionGroup_SeedsPrincipalFromConnectionSoIdentityFlowsToTheGuard()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")]));
        var (hub, _, _, _, userContext) = BuildHub(principal);

        await hub.JoinSessionGroup(liveSessionId, teamId);

        userContext.Principal.Should().BeSameAs(principal);
    }

    [Fact]
    public async Task JoinSessionGroup_NonMember_IsRejectedAndNotAddedToTheGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var (hub, groups, guard, _, _) = BuildHub();
        guard
            .Setup(g => g.EnsureAllowedAsync(liveSessionId, teamId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var act = () => hub.JoinSessionGroup(liveSessionId, teamId);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinSessionGroupAsOperator_AssignedOperator_AddsCallerToTheSameLiveSessionGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var (hub, groups, _, accessResolver, _) = BuildHub();

        await hub.JoinSessionGroupAsOperatorAsync(liveSessionId);

        accessResolver.Verify(
            r => r.EnsureAccessAsync(liveSessionId, CancellationToken.None),
            Times.Once);
        // The same group the participant join uses — RankingChanged is broadcast there.
        groups.Verify(
            g => g.AddToGroupAsync("connection-1", $"live-session:{liveSessionId:D}", CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task JoinSessionGroupAsOperator_NeverConsultsTheTeamMembershipGuard()
    {
        var liveSessionId = Guid.NewGuid();
        var (hub, _, guard, _, _) = BuildHub();

        await hub.JoinSessionGroupAsOperatorAsync(liveSessionId);

        // An operator is a member of no team; consulting the membership guard would always deny.
        guard.Verify(
            g => g.EnsureAllowedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinSessionGroupAsOperator_SeedsPrincipalFromConnectionSoIdentityFlowsToTheResolver()
    {
        var liveSessionId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "operator-1")]));
        var (hub, _, _, _, userContext) = BuildHub(principal);

        await hub.JoinSessionGroupAsOperatorAsync(liveSessionId);

        userContext.Principal.Should().BeSameAs(principal);
    }

    [Fact]
    public async Task JoinSessionGroupAsOperator_UnassignedOperator_IsRejectedAndNotAddedToTheGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var (hub, groups, _, accessResolver, _) = BuildHub();
        accessResolver
            .Setup(r => r.EnsureAccessAsync(liveSessionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException());

        var act = () => hub.JoinSessionGroupAsOperatorAsync(liveSessionId);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LeaveSessionGroup_RemovesCallerConnectionFromTheLiveSessionGroup()
    {
        var liveSessionId = Guid.NewGuid();
        var (hub, groups, _, _, _) = BuildHub();

        await hub.LeaveSessionGroup(liveSessionId);

        groups.Verify(
            g => g.RemoveFromGroupAsync("connection-1", $"live-session:{liveSessionId:D}", CancellationToken.None),
            Times.Once);
    }
}
