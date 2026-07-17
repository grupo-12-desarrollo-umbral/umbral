using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Exceptions;
using umbral_backend.Domain.Services;
using umbral_backend.SessionOperations.UnitTests.Domain.TestData;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

/// <summary>
/// Treasure-hunt substage completion (D-1/D-2) and the mission deadline ending a session (D-4).
/// Before this, a treasure-hunt substage could never end: nothing checked whether it was cleared, and
/// the only caller of substage advancement sat behind trivia question exhaustion — so a mission that
/// alternated play modes hung forever at the hunt.
/// </summary>
public sealed class LiveSessionSubstageCompletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 3, 10, 1, 0, TimeSpan.Zero);

    // ── D-1: first clear ends the substage ────────────────────────────────────────────────────────

    [Fact]
    public void RegisterTargetScan_WhenLastActiveTargetResolved_BeginsRankingReveal()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();
        var substageId = session.ActiveSubstageId!.Value;

        session.RegisterTargetScan(team.TeamId, "QR-001", Guid.NewGuid(), Now);
        session.RegisterTargetScan(team.TeamId, "QR-002", Guid.NewGuid(), Now.AddSeconds(1));

        // Two of three: not cleared yet.
        session.IsSubstageClearedBy(team.TeamId, substageId).Should().BeFalse();
        session.IsAwaitingSubstageRankingReveal.Should().BeFalse();

        session.RegisterTargetScan(team.TeamId, "QR-003", Guid.NewGuid(), Now.AddSeconds(2));

        session.IsSubstageClearedBy(team.TeamId, substageId).Should().BeTrue();
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        session.SubstageRevealUntil.Should()
            .Be(Now.AddSeconds(2) + LiveSession.SubstageRankingRevealDuration);

        var revealEvent = session.DomainEvents.OfType<SubstageRevealStartedEvent>().Should().ContainSingle().Subject;
        revealEvent.SubstageSnapshotId.Should().Be(substageId);
        revealEvent.PlayMode.Should().Be(SubstagePlayMode.TreasureHunt);
        // Single-substage mission: this ranking is where the mission ends.
        revealEvent.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void RegisterTargetScan_WhenSubstageCleared_StillScoresTheClearingTarget()
    {
        // The clear is evaluated AFTER the accept, so the target that completes the set is scored like
        // any other. A clear that swallowed its own last target's score would be a silent point loss.
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();

        session.RegisterTargetScan(team.TeamId, "QR-001", Guid.NewGuid(), Now);
        session.RegisterTargetScan(team.TeamId, "QR-002", Guid.NewGuid(), Now.AddSeconds(1));
        var clearing = session.RegisterTargetScan(team.TeamId, "QR-003", Guid.NewGuid(), Now.AddSeconds(2));

        clearing.ValidationState.Should().Be(EvidenceValidationState.Accepted);
        session.DomainEvents.OfType<TargetResolvedEvent>().Should().HaveCount(3);
        session.DomainEvents.OfType<TargetResolvedEvent>().Last().ScoreValue.Should().Be(200);
    }

    [Fact]
    public void IsSubstageClearedBy_WhenSubstageHasNoActiveTargets_IsNotCleared()
    {
        // A hunt with nothing to scan is unreachable, not instantly complete — the runtime deliberately
        // has no "zero targets = cleared" fallback, because D-7 rejects such a mission at authoring
        // where a human can actually fix it.
        var session = LiveSessionFactory.CreateScheduledTrivia();
        Activate(session);

        session.IsSubstageClearedBy(session.Teams.Single().TeamId, session.ActiveSubstageId!.Value)
            .Should()
            .BeFalse();
    }

    // ── D-2: hard cut on clear ────────────────────────────────────────────────────────────────────

    [Fact]
    public void RegisterTargetScan_WhenAnotherTeamAlreadyCleared_RejectsTheInFlightScan()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        ActivateWithTwoTeams(session);
        var (winner, loser) = (session.Teams.First(), session.Teams.Last());

        // The loser banks one target before the winner clears — that score must survive the cut.
        session.RegisterTargetScan(loser.TeamId, "QR-001", Guid.NewGuid(), Now);

        session.RegisterTargetScan(winner.TeamId, "QR-001", Guid.NewGuid(), Now.AddSeconds(1));
        session.RegisterTargetScan(winner.TeamId, "QR-002", Guid.NewGuid(), Now.AddSeconds(2));
        session.RegisterTargetScan(winner.TeamId, "QR-003", Guid.NewGuid(), Now.AddSeconds(3));
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();

        // The loser's scan was in flight when the winner cleared. It is dead on arrival.
        var inFlight = session.RegisterTargetScan(loser.TeamId, "QR-002", Guid.NewGuid(), Now.AddSeconds(4));

        inFlight.ValidationState.Should().Be(EvidenceValidationState.Rejected);
        inFlight.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.SubstageAlreadyCleared);
        // Retained for audit, and the loser keeps QR-001's score.
        session.TreasureEvidenceSubmissions.Should().Contain(inFlight);
        session.ProjectParticipantTeamBoard(loser.TeamId, Now.AddSeconds(4))
            .ActiveSubstageContext!.ResolvedTargets.Should().Be(1);
    }

    [Fact]
    public void RegisterTargetScan_WhenClearingTeamRescansItsOwnTarget_ReportsTheDuplicateNotTheCut()
    {
        // The cleared check runs last, so the precise diagnosis still wins. This is also what keeps
        // "another team already completed this stage" honest: only a team that did not clear reaches it.
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);
        var team = session.Teams.Single();

        session.RegisterTargetScan(team.TeamId, "QR-001", Guid.NewGuid(), Now);
        session.RegisterTargetScan(team.TeamId, "QR-002", Guid.NewGuid(), Now.AddSeconds(1));
        session.RegisterTargetScan(team.TeamId, "QR-003", Guid.NewGuid(), Now.AddSeconds(2));

        var duplicate = session.RegisterTargetScan(team.TeamId, "QR-001", Guid.NewGuid(), Now.AddSeconds(3));

        duplicate.ResolutionRejectionReason.Should()
            .Be(TargetResolutionRejectionReason.TargetAlreadyResolvedByTeam);
    }

    [Fact]
    public void BeginSubstageRankingReveal_WhenTwoTeamsClearInTheSameTick_IsIdempotent()
    {
        // The ranking on screen is the settled result and must not move while displayed (D-2), so the
        // second clear must not restart the window. The xmin token guards the same race at the DB; this
        // guards it in the aggregate.
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        ActivateWithTwoTeams(session);
        var (first, second) = (session.Teams.First(), session.Teams.Last());

        foreach (var qr in new[] { "QR-001", "QR-002", "QR-003" })
        {
            session.RegisterTargetScan(first.TeamId, qr, Guid.NewGuid(), Now);
        }

        var revealUntil = session.SubstageRevealUntil;
        session.BeginSubstageRankingReveal(Now.AddSeconds(5), LiveSession.SubstageRankingRevealDuration);

        session.SubstageRevealUntil.Should().Be(revealUntil);
        session.DomainEvents.OfType<SubstageRevealStartedEvent>().Should().ContainSingle();
        second.Should().NotBeNull();
    }

    [Fact]
    public void CompleteSubstageRankingReveal_WhenNoRevealIsPending_Throws()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);

        var act = () => session.CompleteSubstageRankingReveal();

        act.Should().Throw<NoActiveSubstageRevealException>();
    }

    // ── D-4: the mission deadline ends the session ────────────────────────────────────────────────

    [Fact]
    public void FinishOnMissionDeadline_FinishesWithoutClaimingTheSubstageCompleted()
    {
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);
        var substageId = session.ActiveSubstageId;
        var expiredAt = Now.AddMinutes(45);

        session.FinishOnMissionDeadline(expiredAt);

        session.State.Should().Be(SessionState.Finished);
        // The substage did not complete — SubstageAdvancedEvent would say it did.
        session.ActiveSubstageId.Should().Be(substageId);
        session.DomainEvents.OfType<SubstageAdvancedEvent>().Should().BeEmpty();
        var deadlineEvent = session.DomainEvents.OfType<MissionDeadlineReachedEvent>().Should().ContainSingle().Subject;
        deadlineEvent.ActiveSubstageId.Should().Be(substageId);
        session.DomainEvents.OfType<SessionStateChangedEvent>().Last()
            .CurrentState.Should().Be(SessionState.Finished);
    }

    [Fact]
    public void FinishOnMissionDeadline_WhenSessionIsPaused_Throws()
    {
        // A paused session's deadline is frozen, so it cannot expire — and the worker never ticks it.
        var session = LiveSessionFactory.CreateScheduledMultiTargetTreasureHunt();
        Activate(session);
        session.MoveTo(SessionState.Paused, Now.AddMinutes(1), new SessionStateTransitionPolicy());

        var act = () => session.FinishOnMissionDeadline(Now.AddMinutes(45));

        act.Should().Throw<DomainException>();
    }

    private static void ActivateWithTwoTeams(LiveSession session)
    {
        session.AssociateTeam(Guid.NewGuid(), "Beta", "B-01", 4);
        Activate(session);
    }

    private static void Activate(LiveSession session)
    {
        var policy = new SessionStateTransitionPolicy();
        var preparingAt = new DateTimeOffset(2026, 6, 3, 10, 0, 0, TimeSpan.Zero);

        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        session.MoveTo(SessionState.Preparing, preparingAt, policy);
        session.MoveTo(SessionState.Active, preparingAt.AddMinutes(1), policy);
    }
}
