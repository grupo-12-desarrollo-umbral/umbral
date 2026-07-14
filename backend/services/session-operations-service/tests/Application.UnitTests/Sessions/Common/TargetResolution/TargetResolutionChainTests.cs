using umbral_backend.Application.Sessions.Common.TargetResolution;
using umbral_backend.Application.Sessions.Common.TargetResolution.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Common.TargetResolution;

public sealed class TargetResolutionChainTests
{
    [Fact]
    public async Task ValidateAsync_RunsInOrderAndShortCircuitsAtFirstReason()
    {
        var calls = new List<string>();
        var session = ActiveSession(out var teamId);
        var chain = new TargetResolutionChain(new TargetResolutionLink[]
        {
            new RecordingLink("exists", calls, TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget),
            new RecordingLink("belongs", calls),
            new RecordingLink("duplicate", calls)
        });

        var reason = await chain.ValidateAsync(
            new TargetResolutionContext(session, teamId, session.ActiveSubstageId!.Value, "bad"),
            CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget);
        calls.Should().Equal("exists");
    }

    [Fact]
    public async Task RealLinks_InvalidQr_ReturnFirstCanonicalReason()
    {
        var session = ActiveSession(out var teamId);
        var reason = await RealChain().ValidateAsync(
            Context(session, teamId, "unknown"), CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.ScannedValueDoesNotResolveToTarget);
    }

    [Fact]
    public async Task RealLinks_TargetOutsideDeclaredActiveSubstage_ReturnOwnershipReason()
    {
        var session = ActiveSession(out var teamId);
        var reason = await RealChain().ValidateAsync(
            new TargetResolutionContext(session, teamId, Guid.NewGuid(), "QR-001"), CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.TargetOutsideActiveSubstage);
    }

    [Fact]
    public async Task RealLinks_AlreadyResolvedByTeam_ReturnDuplicateReason()
    {
        var session = ActiveSession(out var teamId);
        session.RegisterTargetScan(teamId, "QR-001", Guid.NewGuid(), DateTimeOffset.UtcNow);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, "QR-001"), CancellationToken.None);

        reason.Should().Be(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam);
    }

    [Fact]
    public async Task RealLinks_ResolvableActiveUnresolvedTarget_Accepts()
    {
        var session = ActiveSession(out var teamId);

        var reason = await RealChain().ValidateAsync(Context(session, teamId, " qr-001 "), CancellationToken.None);

        reason.Should().BeNull();
    }

    private static TargetResolutionChain RealChain() => new(new TargetResolutionLink[]
    {
        new TargetExistsForScanLink(),
        new TargetBelongsToActiveSubstageLink(),
        new TargetNotAlreadyResolvedLink()
    });

    private static TargetResolutionContext Context(LiveSession session, Guid teamId, string scan) =>
        new(session, teamId, session.ActiveSubstageId!.Value, scan);

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

    private sealed class RecordingLink : TargetResolutionLink
    {
        private readonly string _name;
        private readonly List<string> _calls;
        private readonly TargetResolutionRejectionReason? _reason;

        public RecordingLink(
            string name,
            List<string> calls,
            TargetResolutionRejectionReason? reason = null)
        {
            _name = name;
            _calls = calls;
            _reason = reason;
        }

        protected override Task<TargetResolutionRejectionReason?> CheckAsync(
            TargetResolutionContext context,
            CancellationToken cancellationToken)
        {
            _calls.Add(_name);
            return Task.FromResult(_reason);
        }
    }
}
