using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using umbral_backend.Application.Common.Interfaces;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Events;
using umbral_backend.Domain.Services;
using umbral_backend.Domain.ValueObjects;
using umbral_backend.Infrastructure.Realtime;

namespace umbral_backend.Infrastructure.IntegrationTests.Infrastructure;

/// <summary>
/// Drives the timer worker through a mixed-mode mission (Trivia -> TreasureHunt -> Trivia) to Finished
/// with real orchestration collaborators — the test the spec names as the one that would have caught
/// the hang. Every existing mixed-mode test called <c>CompleteActiveSubstageAndAdvance</c> straight on
/// the domain, bypassing the runtime entirely, so they all passed while the runtime was dead. Nothing
/// below touches the domain's advancement directly: the worker's own branches have to do it.
///
/// The repository is a stub over one in-memory aggregate; the facade, activator and coordinator are
/// real. That is the seam under test — advancement being reachable from BOTH play modes.
/// </summary>
public sealed class MixedModeSessionWorkerDriveTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Worker_DrivesTriviaThenTreasureHuntThenTriviaToFinished_WithARankingRevealBetweenEachSubstage()
    {
        var session = CreateActiveMixedSession(maximumTimeMinutes: 90);
        var harness = new WorkerHarness(session);
        var substages = OrderedSubstages(session);
        var team = session.Teams.Single();

        // ── Substage 1: trivia. Its single question runs out. ────────────────────────────────────
        session.ActivateQuestion(0, Start);
        var now = Start.AddSeconds(31); // past the 30s question window
        await harness.TickAt(now);

        // 5s answer reveal is open; the substage has NOT ended yet.
        session.IsAwaitingQuestionReveal.Should().BeTrue();
        session.IsAwaitingSubstageRankingReveal.Should().BeFalse();

        now = now.Add(TriviaRoundOrchestratorFacade.QuestionRevealDuration);
        await harness.TickAt(now);

        // Answer reveal done -> the substage's last question is exhausted -> 10s ranking (D-3). The
        // pointer must NOT have moved yet: advance used to be immediate here.
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        session.ActiveSubstageId.Should().Be(substages[0].SubstageSnapshotId);

        // A tick inside the reveal window advances nothing — the ranking must sit still (D-2).
        await harness.TickAt(now.AddSeconds(3));
        session.ActiveSubstageId.Should().Be(substages[0].SubstageSnapshotId);

        now = now.Add(LiveSession.SubstageRankingRevealDuration);
        await harness.TickAt(now);

        // ── Substage 2: the treasure hunt. THIS is where the session used to hang forever. ───────
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().BeNull();
        session.State.Should().Be(SessionState.Active);

        // Ticks alone will never move a hunt on — it ends when a team clears it (D-1), not on a clock.
        await harness.TickAt(now.AddMinutes(1));
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);

        var clearedAt = now.AddMinutes(2);
        session.RegisterTargetScan(team.TeamId, "QR-ALPHA", Guid.NewGuid(), clearedAt);
        session.RegisterTargetScan(team.TeamId, "QR-BETA", Guid.NewGuid(), clearedAt);

        // The scan itself opens the reveal, straight from the domain — no facade in the path.
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);

        now = clearedAt.Add(LiveSession.SubstageRankingRevealDuration);
        await harness.TickAt(now);

        // ── Substage 3: trivia again. Advancing OUT of a hunt is the other half of the bug. ──────
        session.ActiveSubstageId.Should().Be(substages[2].SubstageSnapshotId);
        session.ActiveQuestionIndex.Should().Be(0);
        session.State.Should().Be(SessionState.Active);

        now = now.AddSeconds(31);
        await harness.TickAt(now);
        now = now.Add(TriviaRoundOrchestratorFacade.QuestionRevealDuration);
        await harness.TickAt(now);

        // Last substage: the ranking is terminal — the session finishes ON it, not before it.
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();
        session.State.Should().Be(SessionState.Active);

        now = now.Add(LiveSession.SubstageRankingRevealDuration);
        await harness.TickAt(now);

        session.State.Should().Be(SessionState.Finished);
        // One reveal per substage, both modes — asserted on the domain fact rather than the broadcast,
        // since the push hangs off event dispatch (a real SaveChanges) that this stub repository has no
        // part in. BroadcastSubstageRevealStartedNotificationHandlerTests covers the push itself.
        session.DomainEvents.OfType<SubstageRevealStartedEvent>().Should().HaveCount(substages.Length);
        session.DomainEvents.OfType<SubstageRevealStartedEvent>()
            .Select(revealEvent => revealEvent.PlayMode)
            .Should()
            .Equal(SubstagePlayMode.Trivia, SubstagePlayMode.TreasureHunt, SubstagePlayMode.Trivia);
        // Only the last one ends the mission.
        session.DomainEvents.OfType<SubstageRevealStartedEvent>()
            .Select(revealEvent => revealEvent.IsTerminal)
            .Should()
            .Equal(false, false, true);
    }

    [Fact]
    public async Task Worker_WhenMissionDeadlineExpiresDuringATreasureHunt_FinishesTheSession()
    {
        // The hang's other face: before D-4 the deadline was report-only, so a hunt nobody cleared ran
        // forever. MaximumTime is now one mission-wide budget that ends the session wherever play is.
        var session = CreateActiveMixedSession(maximumTimeMinutes: 90);
        var harness = new WorkerHarness(session);
        var substages = OrderedSubstages(session);

        await AdvanceOutOfFirstTriviaSubstage(session, harness);
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);

        // Nobody ever clears the hunt. The deadline runs out on it.
        await harness.TickAt(Start.AddMinutes(91));

        session.State.Should().Be(SessionState.Finished);
        // The hunt did not complete — the pointer stays where play stopped.
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
    }

    [Fact]
    public async Task Worker_WhenMissionDeadlineExpiresMidRankingReveal_FinishesOnTheRankingWithoutAdvancing()
    {
        // D-6: the reveal branch is checked before mission expiry, so a deadline landing mid-reveal
        // plays the reveal out and then ends the session ON that ranking. Advancing into the next
        // substage first would flash its play surface for the gap before Finished lands — so the reveal
        // completion decides finish-vs-advance atomically and finishes directly here.
        var session = CreateActiveMixedSession(maximumTimeMinutes: 90);
        var harness = new WorkerHarness(session);
        var substages = OrderedSubstages(session);
        var team = session.Teams.Single();

        await AdvanceOutOfFirstTriviaSubstage(session, harness);

        // Clear the hunt 2 seconds before the deadline: the 10s reveal outlives MaximumTime.
        var clearedAt = Start.AddMinutes(90).AddSeconds(-2);
        session.RegisterTargetScan(team.TeamId, "QR-ALPHA", Guid.NewGuid(), clearedAt);
        session.RegisterTargetScan(team.TeamId, "QR-BETA", Guid.NewGuid(), clearedAt);
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();

        // A tick past the deadline but inside the reveal: the reveal wins, the session survives.
        await harness.TickAt(Start.AddMinutes(90).AddSeconds(1));
        session.State.Should().Be(SessionState.Active);
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);

        // Once the reveal elapses the elapsed deadline finishes the session directly — no advance into
        // the third substage, no SubstageAdvanced off the hunt, the pointer stays where play stopped.
        await harness.TickAt(clearedAt.Add(LiveSession.SubstageRankingRevealDuration));
        session.State.Should().Be(SessionState.Finished);
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.DomainEvents.OfType<SubstageAdvancedEvent>()
            .Should().NotContain(advanced => advanced.FromSubstageId == substages[1].SubstageSnapshotId);
        session.DomainEvents.OfType<MissionDeadlineReachedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Worker_WhenMissionDeadlineLandsExactlyAtRevealCompletion_FinishesOnTheRanking()
    {
        // The boundary case: the deadline falls exactly on SubstageRevealUntil. The reveal still ends
        // the mission ON its ranking rather than advancing — an exhausted budget is an expiry whether
        // it lands a second before completion or precisely on it.
        var session = CreateActiveMixedSession(maximumTimeMinutes: 90);
        var harness = new WorkerHarness(session);
        var substages = OrderedSubstages(session);
        var team = session.Teams.Single();

        await AdvanceOutOfFirstTriviaSubstage(session, harness);

        // Clear so the 10s reveal window closes exactly at the 90-minute deadline.
        var clearedAt = Start.AddMinutes(90).Subtract(LiveSession.SubstageRankingRevealDuration);
        session.RegisterTargetScan(team.TeamId, "QR-ALPHA", Guid.NewGuid(), clearedAt);
        session.RegisterTargetScan(team.TeamId, "QR-BETA", Guid.NewGuid(), clearedAt);
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();

        await harness.TickAt(Start.AddMinutes(90));

        session.State.Should().Be(SessionState.Finished);
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
    }

    [Fact]
    public async Task Worker_WhenSessionIsPausedDuringARankingReveal_DoesNotAdvanceUntilResumed()
    {
        // The reveal is an absolute deadline and paused sessions are not ticked at all, so a pause holds
        // the ranking on screen and the advance lands on the first tick after resume.
        var session = CreateActiveMixedSession(maximumTimeMinutes: 90);
        var harness = new WorkerHarness(session);
        var substages = OrderedSubstages(session);
        var team = session.Teams.Single();
        var policy = new SessionStateTransitionPolicy();

        await AdvanceOutOfFirstTriviaSubstage(session, harness);

        var clearedAt = Start.AddMinutes(5);
        session.RegisterTargetScan(team.TeamId, "QR-ALPHA", Guid.NewGuid(), clearedAt);
        session.RegisterTargetScan(team.TeamId, "QR-BETA", Guid.NewGuid(), clearedAt);

        session.MoveTo(SessionState.Paused, clearedAt.AddSeconds(1), policy);

        // Well past the reveal deadline, but paused: ListActiveTimersAsync excludes it, so no advance.
        await harness.TickAt(clearedAt.AddMinutes(10));
        session.ActiveSubstageId.Should().Be(substages[1].SubstageSnapshotId);
        session.IsAwaitingSubstageRankingReveal.Should().BeTrue();

        session.MoveTo(SessionState.Active, clearedAt.AddMinutes(10), policy);
        await harness.TickAt(clearedAt.AddMinutes(10).AddSeconds(1));

        session.ActiveSubstageId.Should().Be(substages[2].SubstageSnapshotId);
    }

    // Drives trivia substage 1 (question expiry -> 5s answer reveal -> 10s ranking) to its advance.
    private static async Task AdvanceOutOfFirstTriviaSubstage(LiveSession session, WorkerHarness harness)
    {
        session.ActivateQuestion(0, Start);
        var now = Start.AddSeconds(31);
        await harness.TickAt(now);
        now = now.Add(TriviaRoundOrchestratorFacade.QuestionRevealDuration);
        await harness.TickAt(now);
        await harness.TickAt(now.Add(LiveSession.SubstageRankingRevealDuration));
    }

    private static SubstageSnapshot[] OrderedSubstages(LiveSession session) =>
        session.MissionRuntimeSnapshot.StageSnapshots
            .OrderBy(stage => stage.SequenceOrder)
            .SelectMany(stage => stage.SubstageSnapshots.OrderBy(substage => substage.SequenceOrder))
            .ToArray();

    private static LiveSession CreateActiveMixedSession(int maximumTimeMinutes)
    {
        var sourceMissionId = Guid.NewGuid();
        var trivia1 = SubstageSnapshot.CreateTrivia("Opening Trivia", 1);
        var hunt = SubstageSnapshot.CreateTreasureHunt("Museum Hunt", 2);
        var trivia2 = SubstageSnapshot.CreateTrivia("Closing Trivia", 3);

        var snapshot = MissionRuntimeSnapshot.Create(
            sourceMissionId,
            "Mixed Mission",
            MaximumTime.Create(maximumTimeMinutes),
            [StageSnapshot.Create("Stage One", 1, [trivia1, hunt, trivia2])],
            [
                CreateTarget(hunt.SubstageSnapshotId, "Target Alpha", "QR-ALPHA", 1),
                CreateTarget(hunt.SubstageSnapshotId, "Target Beta", "QR-BETA", 2)
            ],
            [
                CreateQuestion(trivia1.SubstageSnapshotId, "Capital of France?", "Paris", "Lyon"),
                CreateQuestion(trivia2.SubstageSnapshotId, "Capital of Peru?", "Lima", "Cusco")
            ]);

        var session = LiveSession.Create(
            SessionSource.Create(sourceMissionId),
            $"SES-{Guid.NewGuid():N}"[..12],
            "Mixed Mode Session",
            maximumTimeMinutes,
            Start.AddMinutes(-10),
            snapshot);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);

        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Start.AddMinutes(-2), policy);
        // Seeds the mission deadline from MaximumTime and points at the first substage.
        session.MoveTo(SessionState.Active, Start, policy);
        return session;
    }

    private static TargetSnapshot CreateTarget(Guid substageId, string name, string qrCode, int order) =>
        TargetSnapshot.Create(substageId, name, qrCode, order, true, 100, 4.711, -74.0721, "A clue", "VisibleAtStart");

    private static TriviaQuestionSnapshot CreateQuestion(Guid substageId, string prompt, string right, string wrong) =>
        TriviaQuestionSnapshot.Create(
            substageId,
            prompt,
            1,
            50,
            30,
            null,
            [TriviaOptionSnapshot.Create(right, 1, true), TriviaOptionSnapshot.Create(wrong, 2, false)]);

    // Real facade/activator/coordinator over a stub repository holding one in-memory aggregate. The
    // ListActiveTimersAsync stub mirrors the real predicate's State == Active filter, which is what
    // makes the pause case above honest rather than assumed.
    private sealed class WorkerHarness
    {
        private readonly AuthoritativeSessionTimerWorker _worker;
        private readonly LiveSession _session;
        private DateTimeOffset _now;

        public WorkerHarness(LiveSession session)
        {
            _session = session;
            _now = Start;

            var repository = new Mock<ILiveSessionRepository>();
            repository
                .Setup(repo => repo.ListActiveTimersAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => session.State == SessionState.Active ? [session] : Array.Empty<LiveSession>());
            repository
                .Setup(repo => repo.UpdateAsync(It.IsAny<LiveSession>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var questionBroadcaster = new Mock<ISessionQuestionBroadcaster>();

            var timerBroadcaster = new Mock<ISessionTimerBroadcaster>();

            var activator = new QuestionActivator(
                repository.Object,
                questionBroadcaster.Object,
                new SequentialQuestionActivationStrategy());
            var coordinator = new SubstageAdvanceCoordinator(
                repository.Object,
                questionBroadcaster.Object,
                activator,
                new SessionStateTransitionPolicy());
            var facade = new TriviaRoundOrchestratorFacade(
                repository.Object,
                questionBroadcaster.Object,
                new SequentialQuestionActivationStrategy(),
                activator,
                coordinator);

            var services = new ServiceCollection();
            services.AddScoped(_ => repository.Object);
            services.AddScoped(_ => timerBroadcaster.Object);
            services.AddScoped<ITriviaRoundOrchestratorFacade>(_ => facade);
            services.AddScoped<ISubstageAdvanceCoordinator>(_ => coordinator);
            var provider = services.BuildServiceProvider();

            _worker = new AuthoritativeSessionTimerWorker(
                provider.GetRequiredService<IServiceScopeFactory>(),
                new MovableTimeProvider(() => _now),
                NullLogger<AuthoritativeSessionTimerWorker>.Instance);
        }

        public Task TickAt(DateTimeOffset now)
        {
            _now = now;
            _session.Should().NotBeNull();
            return _worker.TickAsync(CancellationToken.None);
        }
    }

    private sealed class MovableTimeProvider(Func<DateTimeOffset> utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow();
    }
}
