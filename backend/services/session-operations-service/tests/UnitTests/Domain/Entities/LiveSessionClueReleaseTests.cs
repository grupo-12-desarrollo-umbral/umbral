using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class LiveSessionClueReleaseTests
{
    private static readonly DateTimeOffset ReleasedAt = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReleaseClue_AppendsManualRecordIncrementsTeamAndRaisesEvent()
    {
        var session = CreateActiveSessionWithHiddenClue(out var target, out var alpha, out _);

        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), alpha.TeamId, operatorUserId: 42, now: ReleasedAt);

        var record = session.GetClueReleaseRecords().Should().ContainSingle().Which;
        record.ClueReleaseRecordId.Should().NotBeEmpty();
        record.LiveSessionId.Should().Be(session.LiveSessionId);
        record.TeamId.Should().Be(alpha.TeamId);
        record.TargetId.Should().Be(target.TargetSnapshotId);
        record.ClueId.Should().BeNull("the target snapshot embeds the clue and has no distinct clue node id");
        record.ReleaseMode.Should().Be(ReleaseMode.Manual);
        record.ReleasedByUserId.Should().Be(42);
        record.ReleasedAt.Should().Be(ReleasedAt);
        alpha.ReleasedClueCount.Should().Be(1);

        var releasedEvent = session.DomainEvents.OfType<ClueReleasedEvent>().Should().ContainSingle().Which;
        releasedEvent.LiveSessionId.Should().Be(session.LiveSessionId);
        releasedEvent.TeamId.Should().Be(alpha.TeamId);
        releasedEvent.TargetId.Should().Be(target.TargetSnapshotId);
        releasedEvent.ClueId.Should().BeNull();
        releasedEvent.ReleaseMode.Should().Be(ReleaseMode.Manual);
        releasedEvent.ReleasedByUserId.Should().Be(42);
        releasedEvent.ReleasedAt.Should().Be(ReleasedAt);
    }

    [Fact]
    public void ReleaseClue_WhenSessionIsNotActive_ThrowsWithoutWriting()
    {
        var session = CreateScheduledSessionWithHiddenClue(out var target);
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        var act = () => session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), team.TeamId, 42, ReleasedAt);

        act.Should().Throw<SessionNotActiveForClueReleaseException>()
            .Which.Category.Should().Be(ErrorCategory.Conflict);
        session.GetClueReleaseRecords().Should().BeEmpty();
    }

    [Fact]
    public void ReleaseClue_WhenTargetHasNoHiddenClue_ThrowsWithoutWriting()
    {
        var session = LiveSessionFactory.CreateScheduledTreasureHunt();
        var team = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        Activate(session);
        var target = session.MissionRuntimeSnapshot.TargetSnapshots.Single();

        var act = () => session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), team.TeamId, 42, ReleasedAt);

        act.Should().Throw<ClueNotReleasableException>()
            .Which.Category.Should().Be(ErrorCategory.Conflict);
        session.GetClueReleaseRecords().Should().BeEmpty();
    }

    [Fact]
    public void ReleaseClue_WhenOperatorUserIdIsNotPositive_ThrowsWithoutWriting()
    {
        var session = CreateActiveSessionWithHiddenClue(out var target, out var alpha, out _);

        var act = () => session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), alpha.TeamId, 0, ReleasedAt);

        act.Should().Throw<OperatorUserIdMustBePositiveException>();
        session.GetClueReleaseRecords().Should().BeEmpty();
    }

    [Fact]
    public void ReleaseClue_WhenAlreadyReleasedToSameTeamAndTarget_RejectsDuplicate()
    {
        var session = CreateActiveSessionWithHiddenClue(out var target, out var alpha, out _);
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), alpha.TeamId, 42, ReleasedAt);

        var act = () => session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), alpha.TeamId, 42, ReleasedAt.AddMinutes(1));

        act.Should().Throw<ClueAlreadyReleasedToTeamException>()
            .Which.Category.Should().Be(ErrorCategory.Conflict);
        session.GetClueReleaseRecords().Should().ContainSingle();
        alpha.ReleasedClueCount.Should().Be(1);
        session.DomainEvents.OfType<ClueReleasedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void ReleaseClueToAllTeams_AppendsOneRecordAndEventPerTeam()
    {
        var session = CreateActiveSessionWithHiddenClue(out var target, out var alpha, out var bravo);

        session.ReleaseClueToAllTeams(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), 42, ReleasedAt);

        session.GetClueReleaseRecords().Should().HaveCount(2);
        session.GetClueReleaseRecords().Select(record => record.TeamId)
            .Should().BeEquivalentTo([alpha.TeamId, bravo.TeamId]);
        session.DomainEvents.OfType<ClueReleasedEvent>().Should().HaveCount(2);
        alpha.ReleasedClueCount.Should().Be(1);
        bravo.ReleasedClueCount.Should().Be(1);
    }

    [Fact]
    public void ReleaseClueToTeam_TriviaClue_SurfacesOnlyForReleasedTeam()
    {
        var session = CreateActiveTriviaSessionWithHiddenClue(out var clue, out var alpha, out var bravo);
        var subject = ClueReleaseSubject.ForSubstageClue(clue.ClueSnapshotId);

        session.ReleaseClueToTeam(subject, alpha.TeamId, 42, ReleasedAt);

        var record = session.GetClueReleaseRecords().Should().ContainSingle().Which;
        record.TargetId.Should().BeNull();
        record.ClueId.Should().Be(clue.ClueSnapshotId);
        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt).VisibleClues
            .Should().ContainSingle(visible => visible.ClueSnapshotId == clue.ClueSnapshotId);
        session.ProjectParticipantTeamBoard(bravo.TeamId, ReleasedAt).VisibleClues
            .Should().NotContain(visible => visible.ClueSnapshotId == clue.ClueSnapshotId);

        var releasedEvent = session.DomainEvents.OfType<ClueReleasedEvent>().Should().ContainSingle().Which;
        releasedEvent.TargetId.Should().BeNull();
        releasedEvent.ClueId.Should().Be(clue.ClueSnapshotId);
    }

    [Fact]
    public void ReleaseClueToAllTeams_TriviaClue_SurfacesForEveryTeam()
    {
        var session = CreateActiveTriviaSessionWithHiddenClue(out var clue, out var alpha, out var bravo);

        session.ReleaseClueToAllTeams(
            ClueReleaseSubject.ForSubstageClue(clue.ClueSnapshotId),
            42,
            ReleasedAt);

        session.GetClueReleaseRecords().Should().HaveCount(2);
        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt).VisibleClues
            .Should().ContainSingle(visible => visible.ClueSnapshotId == clue.ClueSnapshotId);
        session.ProjectParticipantTeamBoard(bravo.TeamId, ReleasedAt).VisibleClues
            .Should().ContainSingle(visible => visible.ClueSnapshotId == clue.ClueSnapshotId);
    }

    [Fact]
    public void ReleaseClueToTeam_TriviaClue_WhenAlreadyReleased_RejectsDuplicate()
    {
        var session = CreateActiveTriviaSessionWithHiddenClue(out var clue, out var alpha, out _);
        var subject = ClueReleaseSubject.ForSubstageClue(clue.ClueSnapshotId);
        session.ReleaseClueToTeam(subject, alpha.TeamId, 42, ReleasedAt);

        var act = () => session.ReleaseClueToTeam(
            subject,
            alpha.TeamId,
            42,
            ReleasedAt.AddMinutes(1));

        act.Should().Throw<ClueAlreadyReleasedToTeamException>();
        session.GetClueReleaseRecords().Should().ContainSingle();
    }

    [Fact]
    public void ProjectOperatorSessionPanel_ReleasedTriviaClue_CountsItOnce()
    {
        var session = CreateActiveTriviaSessionWithHiddenClue(out var clue, out var alpha, out _);
        session.ReleaseClueToTeam(
            ClueReleaseSubject.ForSubstageClue(clue.ClueSnapshotId),
            alpha.TeamId,
            42,
            ReleasedAt);

        var progress = session.ProjectOperatorSessionPanel(ReleasedAt).TeamProgress
            .Single(team => team.TeamId == alpha.TeamId);

        progress.ReleasedClueCount.Should().Be(2,
            "the initial clue and released hidden clue are disjoint and each count once");
    }

    [Fact]
    public void ReleaseClue_SurfacesHiddenClueOnlyForReleasedTeam()
    {
        var session = CreateActiveSessionWithHiddenClue(out var target, out var alpha, out var bravo);

        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt).VisibleClues.Should().BeEmpty();
        session.ProjectParticipantTeamBoard(bravo.TeamId, ReleasedAt).VisibleClues.Should().BeEmpty();

        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), alpha.TeamId, 42, ReleasedAt);

        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt).VisibleClues
            .Should().ContainSingle().Which.TargetSnapshotId.Should().Be(target.TargetSnapshotId);
        session.ProjectParticipantTeamBoard(bravo.TeamId, ReleasedAt).VisibleClues.Should().BeEmpty();
    }

    [Fact]
    public void ReleaseClue_DoesNotAdvanceSubstageOrResolveTarget()
    {
        var session = CreateActiveSessionWithHiddenClue(out var target, out var alpha, out _);
        var activeSubstageId = session.ActiveSubstageId;

        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(target.TargetSnapshotId), alpha.TeamId, 42, ReleasedAt);

        session.ActiveSubstageId.Should().Be(activeSubstageId);
        var board = session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt);
        board.ActiveSubstageContext!.ResolvedTargets.Should().Be(0);
        session.DomainEvents.OfType<SubstageAdvancedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void ProjectParticipantTeamBoard_OrdersReleasedCluesNewestReleasedFirst()
    {
        var session = CreateActiveSessionWithHiddenTargets(out var targets, out var alpha, out _);

        // Release out of sequence at distinct instants: target 1, then 3, then 2.
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(targets[0].TargetSnapshotId), alpha.TeamId, 42, ReleasedAt);
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(targets[2].TargetSnapshotId), alpha.TeamId, 42, ReleasedAt.AddMinutes(1));
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(targets[1].TargetSnapshotId), alpha.TeamId, 42, ReleasedAt.AddMinutes(2));

        // Newest release sits at the top, oldest at the bottom — regardless of SequenceOrder.
        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt.AddMinutes(3)).VisibleClues
            .Select(clue => clue.TargetSnapshotId)
            .Should().Equal(
                targets[1].TargetSnapshotId,
                targets[2].TargetSnapshotId,
                targets[0].TargetSnapshotId);
    }

    [Fact]
    public void ProjectParticipantTeamBoard_SameInstantReleases_FallBackToSequenceOrder()
    {
        var session = CreateActiveSessionWithHiddenTargets(out var targets, out var alpha, out _);

        // All-teams releases at one instant stamp identical ReleasedAt; SequenceOrder breaks the tie.
        session.ReleaseClueToAllTeams(ClueReleaseSubject.ForTarget(targets[2].TargetSnapshotId), 42, ReleasedAt);
        session.ReleaseClueToAllTeams(ClueReleaseSubject.ForTarget(targets[0].TargetSnapshotId), 42, ReleasedAt);
        session.ReleaseClueToAllTeams(ClueReleaseSubject.ForTarget(targets[1].TargetSnapshotId), 42, ReleasedAt);

        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt).VisibleClues
            .Select(clue => clue.TargetSnapshotId)
            .Should().Equal(
                targets[0].TargetSnapshotId,
                targets[1].TargetSnapshotId,
                targets[2].TargetSnapshotId);
    }

    [Fact]
    public void ProjectParticipantTeamBoard_PinsAlwaysVisibleCluesAboveReleasedClues()
    {
        // Mixed mission: always-visible targets at sequence 1 and 4, hidden targets at sequence 2 and 3.
        var session = CreateActiveSessionWithMixedTargets(out var targets, out var alpha);

        // Always-visible clues surface with no release (they show once the substage starts); the hidden
        // targets stay off the board until released.
        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt).VisibleClues
            .Select(clue => clue.TargetSnapshotId)
            .Should().Equal(
                targets[0].TargetSnapshotId,
                targets[3].TargetSnapshotId);

        // Release hidden target seq 2, then hidden target seq 3 at distinct instants.
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(targets[1].TargetSnapshotId), alpha.TeamId, 42, ReleasedAt);
        session.ReleaseClueToTeam(ClueReleaseSubject.ForTarget(targets[2].TargetSnapshotId), alpha.TeamId, 42, ReleasedAt.AddMinutes(1));

        // Always-visible clues pinned first by sequence (1, 4), then released hidden newest-first (3, 2).
        session.ProjectParticipantTeamBoard(alpha.TeamId, ReleasedAt.AddMinutes(2)).VisibleClues
            .Select(clue => clue.TargetSnapshotId)
            .Should().Equal(
                targets[0].TargetSnapshotId,
                targets[3].TargetSnapshotId,
                targets[2].TargetSnapshotId,
                targets[1].TargetSnapshotId);
    }

    private static LiveSession CreateActiveSessionWithMixedTargets(
        out IReadOnlyList<TargetSnapshot> targets,
        out Team alpha)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [substage]);
        var built = new List<TargetSnapshot>();
        for (var sequence = 1; sequence <= 4; sequence++)
        {
            // Sequences 1 and 4 are always-visible; 2 and 3 stay hidden until released.
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
            Guid.NewGuid(),
            "Museum Hunt",
            MaximumTime.Create(45),
            [stage],
            built,
            []);
        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            "clue-1",
            "Museum Hunt",
            45,
            ReleasedAt.AddHours(-1),
            snapshot);

        alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        Activate(session);
        targets = built;
        return session;
    }

    private static LiveSession CreateActiveSessionWithHiddenTargets(
        out IReadOnlyList<TargetSnapshot> targets,
        out Team alpha,
        out Team bravo)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [substage]);
        var built = new List<TargetSnapshot>();
        for (var sequence = 1; sequence <= 3; sequence++)
        {
            built.Add(TargetSnapshot.Create(
                substage.SubstageSnapshotId,
                $"Target {sequence}",
                $"QR-HIDDEN-{sequence}",
                sequence,
                isActive: true,
                score: 100,
                latitude: 4.711,
                longitude: -74.0721,
                clueText: $"Clue {sequence}.",
                clueVisibilityPolicy: "HiddenUntilOperatorRelease"));
        }

        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Museum Hunt",
            MaximumTime.Create(45),
            [stage],
            built,
            []);
        var session = LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            "clue-1",
            "Museum Hunt",
            45,
            ReleasedAt.AddHours(-1),
            snapshot);

        alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        Activate(session);
        targets = built;
        return session;
    }

    private static LiveSession CreateActiveSessionWithHiddenClue(
        out TargetSnapshot target,
        out Team alpha,
        out Team bravo)
    {
        var session = CreateScheduledSessionWithHiddenClue(out target);
        alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        Activate(session);
        return session;
    }

    private static LiveSession CreateActiveTriviaSessionWithHiddenClue(
        out ClueSnapshot clue,
        out Team alpha,
        out Team bravo)
    {
        var session = LiveSessionFactory.CreateScheduledTriviaWithClues();
        alpha = session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        bravo = session.AssociateTeam(Guid.NewGuid(), "Bravo", "B-01", 4);
        Activate(session);
        clue = session.MissionRuntimeSnapshot.ClueSnapshots
            .Single(snapshot => snapshot.IsHiddenUntilOperatorRelease);
        return session;
    }

    private static LiveSession CreateScheduledSessionWithHiddenClue(out TargetSnapshot target)
    {
        var substage = SubstageSnapshot.CreateTreasureHunt("Treasure Route", 1);
        var stage = StageSnapshot.Create("Stage One", 1, [substage]);
        target = TargetSnapshot.Create(
            substage.SubstageSnapshotId,
            "Main Exhibit",
            "QR-HIDDEN",
            1,
            isActive: true,
            score: 100,
            latitude: 4.711,
            longitude: -74.0721,
            clueText: "Look near the entrance.",
            clueVisibilityPolicy: "HiddenUntilOperatorRelease");
        var snapshot = MissionRuntimeSnapshot.Create(
            Guid.NewGuid(),
            "Museum Hunt",
            MaximumTime.Create(45),
            [stage],
            [target],
            []);

        return LiveSession.Create(
            SessionSource.Create(snapshot.SourceMissionId),
            "clue-1",
            "Museum Hunt",
            45,
            ReleasedAt.AddHours(-1),
            snapshot);
    }

    private static void Activate(LiveSession session)
    {
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, ReleasedAt.AddMinutes(-2), policy);
        session.MoveTo(SessionState.Active, ReleasedAt.AddMinutes(-1), policy);
    }
}
