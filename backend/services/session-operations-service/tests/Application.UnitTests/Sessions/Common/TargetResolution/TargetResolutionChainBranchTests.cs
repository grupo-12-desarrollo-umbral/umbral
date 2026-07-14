using umbral_backend.Application.Sessions.Common.TargetResolution;
using umbral_backend.Application.UnitTests.TestData;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Common.TargetResolution;

// HU-31 chain edge case: an empty link set has no head, so the chain accepts (null verdict) rather than
// dereferencing a missing head. Covers the null-head branch of TargetResolutionChain.ValidateAsync.
public sealed class TargetResolutionChainBranchTests
{
    [Fact]
    public async Task ValidateAsync_WithNoLinks_ReturnsNull()
    {
        var chain = new TargetResolutionChain(Array.Empty<TargetResolutionLink>());
        var session = ActiveSession(out var teamId);

        var reason = await chain.ValidateAsync(
            new TargetResolutionContext(session, teamId, session.ActiveSubstageId!.Value, "QR-001"),
            CancellationToken.None);

        reason.Should().BeNull();
    }

    private static LiveSession ActiveSession(out Guid teamId)
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow, policy);
        teamId = team.TeamId;
        return session;
    }
}
