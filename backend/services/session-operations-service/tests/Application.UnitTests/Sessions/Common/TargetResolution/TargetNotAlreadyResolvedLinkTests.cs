using umbral_backend.Application.Sessions.Common.TargetResolution;
using umbral_backend.Application.Sessions.Common.TargetResolution.Validators;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.Application.UnitTests.Sessions.Common.TargetResolution;

// HU-31 duplicate-guard link (CoR). Exercises every decision branch of the "not already resolved by this
// team" rule directly: team lookup by runtime id vs reference id vs no match, the resolved-target null
// guard, and the accepted-submission predicate on both matching and non-matching submissions.
public sealed class TargetNotAlreadyResolvedLinkTests
{
    private const string QrA = "QR-A";
    private const string QrB = "QR-B";

    [Fact]
    public async Task Validate_WhenTeamAlreadyResolvedTargetByRuntimeId_ReturnsDuplicateReason()
    {
        var session = ActiveTwoTargetSession(out var team1, out _);
        session.RegisterTargetScan(team1.TeamId, QrA, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var reason = await Validate(session, team1.TeamId, QrA);

        reason.Should().Be(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam);
    }

    [Fact]
    public async Task Validate_WhenTeamMatchedByReferenceTeamId_ReturnsDuplicateReason()
    {
        var session = ActiveTwoTargetSession(out var team1, out _);
        session.RegisterTargetScan(team1.TeamId, QrA, Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Callers may present the reference (catalog) team id — the link resolves either identity.
        var reason = await Validate(session, team1.ReferenceTeamId!.Value, QrA);

        reason.Should().Be(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam);
    }

    [Fact]
    public async Task Validate_WhenNoTeamMatchesTheContextTeamId_ReturnsNull()
    {
        var session = ActiveTwoTargetSession(out var team1, out _);
        session.RegisterTargetScan(team1.TeamId, QrA, Guid.NewGuid(), DateTimeOffset.UtcNow);

        var reason = await Validate(session, Guid.NewGuid(), QrA);

        reason.Should().BeNull();
    }

    [Fact]
    public async Task Validate_WhenScannedValueDoesNotResolveToATarget_ReturnsNull()
    {
        var session = ActiveTwoTargetSession(out var team1, out _);

        var reason = await Validate(session, team1.TeamId, "QR-UNKNOWN");

        reason.Should().BeNull();
    }

    [Fact]
    public async Task Validate_WhenSameTeamResolvedADifferentTarget_ReturnsNull()
    {
        var session = ActiveTwoTargetSession(out var team1, out _);
        session.RegisterTargetScan(team1.TeamId, QrA, Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Team1 resolved A; scanning the still-unresolved B is not a duplicate.
        var reason = await Validate(session, team1.TeamId, QrB);

        reason.Should().BeNull();
    }

    [Fact]
    public async Task Validate_WhenAnotherTeamResolvedTheSameTarget_ReturnsNull()
    {
        var session = ActiveTwoTargetSession(out var team1, out var team2);
        session.RegisterTargetScan(team2.TeamId, QrA, Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Team2 resolved A; team1 has not, so team1 scanning A is accepted, not a duplicate.
        var reason = await Validate(session, team1.TeamId, QrA);

        reason.Should().BeNull();
    }

    private static async Task<TargetResolutionRejectionReason?> Validate(
        LiveSession session, Guid teamId, string scannedValue) =>
        await new TargetNotAlreadyResolvedLink().ValidateAsync(
            new TargetResolutionContext(session, teamId, session.ActiveSubstageId!.Value, scannedValue),
            CancellationToken.None);

    // An Active treasure-hunt session whose single active substage carries two resolvable targets
    // (QR-A, QR-B) and two associated teams — enough to exercise every branch of the duplicate guard.
    private static LiveSession ActiveTwoTargetSession(out Team team1, out Team team2)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Two Target Hunt",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [
                TargetSnapshot.Create(substage.SubstageSnapshotId, "Alpha", QrA, 1, true, 100, 0, 0, null, null),
                TargetSnapshot.Create(substage.SubstageSnapshotId, "Beta", QrB, 2, true, 150, 0, 0, null, null)
            ],
            []);

        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId), "hunt-dup", "Hunt", 45,
            DateTimeOffset.UtcNow.AddMinutes(-10), snapshot);
        team1 = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        team2 = session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(-1), policy);
        return session;
    }
}
