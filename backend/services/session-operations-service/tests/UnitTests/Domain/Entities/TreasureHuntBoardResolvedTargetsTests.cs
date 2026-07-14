using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// HU-31 per-team resolved-target counter (BuildTreasureHuntContext). The board must count only the
// calling team's ACCEPTED resolutions of the active substage's ACTIVE targets — excluding another
// team's resolutions, the same team's rejected scans, and inactive targets from the total.
public sealed class TreasureHuntBoardResolvedTargetsTests
{
    [Fact]
    public void ProjectParticipantTeamBoard_CountsOnlyCallerTeamAcceptedResolutionsOfActiveTargets()
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Counter Hunt",
            MaximumTime.Create(45),
            [StageSnapshot.Create("Stage One", 1, [substage])],
            [
                TargetSnapshot.Create(substage.SubstageSnapshotId, "Active One", "QR-001", 1, true, 100, 0, 0, null, null),
                TargetSnapshot.Create(substage.SubstageSnapshotId, "Inactive Two", "QR-002", 2, false, 100, 0, 0, null, null)
            ],
            []);

        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId), "hunt-board", "Hunt", 45,
            DateTimeOffset.UtcNow.AddMinutes(-10), snapshot);
        var teamA = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var teamB = session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, DateTimeOffset.UtcNow.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, DateTimeOffset.UtcNow.AddMinutes(-1), policy);
        var now = DateTimeOffset.UtcNow;

        session.RegisterTargetScan(teamA.TeamId, "QR-001", Guid.NewGuid(), now);          // A accepted
        session.RegisterTargetScan(teamB.TeamId, "QR-001", Guid.NewGuid(), now);          // B accepted (excluded for A)
        session.RegisterTargetScan(teamA.TeamId, "QR-BOGUS", Guid.NewGuid(), now);        // A rejected (excluded)

        var context = session.ProjectParticipantTeamBoard(teamA.TeamId, now).ActiveSubstageContext;

        context.Should().NotBeNull();
        context!.ResolvedTargets.Should().Be(1, "only team A's accepted resolution of an active target counts");
        context.TotalActiveTargets.Should().Be(1, "the inactive target is excluded from the active-target total");
    }
}
