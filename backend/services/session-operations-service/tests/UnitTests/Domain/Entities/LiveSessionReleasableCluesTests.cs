using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

// ProjectReleasableClues backs the operator's release-clue picker for both target-backed treasure-hunt
// clues and target-less trivia clue snapshots.
public sealed class LiveSessionReleasableCluesTests
{
    private static readonly DateTimeOffset At = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProjectReleasableClues_ReturnsOnlyHiddenTargetsOfActiveSubstage_OrderedBySequence()
    {
        var session = CreateActiveSessionWithMixedTargets(out var targets);

        var releasable = session.ProjectReleasableClues();

        // Only the hidden targets (sequences 2 and 3) are releasable; the always-visible 1 and 4 are excluded.
        releasable.Select(clue => clue.TargetId)
            .Should().Equal(targets[1].TargetSnapshotId, targets[2].TargetSnapshotId);
        var first = releasable[0];
        first.ClueId.Should().BeNull();
        first.TargetName.Should().Be("Target 2");
        first.SequenceOrder.Should().Be(2);
        first.ClueText.Should().Be("Clue 2.");
    }

    [Fact]
    public void ProjectReleasableClues_Trivia_ReturnsOnlyHiddenCluesBySequenceOrder()
    {
        var session = LiveSessionFactory.CreateScheduledTriviaWithClues();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        Activate(session);
        var hidden = session.MissionRuntimeSnapshot.ClueSnapshots
            .Single(clue => clue.IsHiddenUntilOperatorRelease);

        var releasable = session.ProjectReleasableClues();

        releasable.Should().ContainSingle();
        releasable[0].TargetId.Should().BeNull();
        releasable[0].ClueId.Should().Be(hidden.ClueSnapshotId);
        releasable[0].TargetName.Should().BeNull();
        releasable[0].SequenceOrder.Should().Be(2);
        releasable[0].ClueText.Should().Be("Released by operator.");
    }

    [Fact]
    public void ProjectReleasableClues_BeforeSessionIsActive_IsEmpty()
    {
        var session = CreateScheduledSessionWithHiddenTarget();

        session.ProjectReleasableClues().Should().BeEmpty();
    }

    private static LiveSession CreateActiveSessionWithMixedTargets(out IReadOnlyList<TargetSnapshot> targets)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [substage]);
        var built = new List<TargetSnapshot>();
        for (var sequence = 1; sequence <= 4; sequence++)
        {
            var policy = sequence is 1 or 4 ? "VisibleWhenSubstageStarts" : "HiddenUntilOperatorRelease";
            built.Add(TargetSnapshot.Create(
                substage.SubstageSnapshotId,
                $"Target {sequence}",
                $"QR-MIXED-{sequence}",
                sequence,
                isActive: true,
                score: 100,
                latitude: 4.711,
                longitude: -74.0721,
                clueText: $"Clue {sequence}.",
                clueVisibilityPolicy: policy));
        }

        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(), "Museum Hunt", MaximumTime.Create(45), [stage], built, []);
        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId), "clue-1", "Museum Hunt", 45, At.AddHours(-1), snapshot);

        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        Activate(session);
        targets = built;
        return session;
    }

    private static LiveSession CreateScheduledSessionWithHiddenTarget()
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [substage]);
        var target = TargetSnapshot.Create(
            substage.SubstageSnapshotId, "Main Exhibit", "QR-HIDDEN", 1, isActive: true, score: 100,
            latitude: 4.711, longitude: -74.0721, clueText: "Look near the entrance.",
            clueVisibilityPolicy: "HiddenUntilOperatorRelease");
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(), "Museum Hunt", MaximumTime.Create(45), [stage], [target], []);

        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId), "clue-1", "Museum Hunt", 45, At.AddHours(-1), snapshot);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        return session;
    }

    private static void Activate(LiveSession session)
    {
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, At.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, At.AddMinutes(-1), policy);
    }
}
