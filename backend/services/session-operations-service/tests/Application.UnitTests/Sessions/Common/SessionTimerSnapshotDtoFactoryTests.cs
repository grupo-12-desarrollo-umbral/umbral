using System.Text.Json;
using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Common;

public sealed class SessionTimerSnapshotDtoFactoryTests
{
    private static readonly DateTimeOffset Now = LiveSessionTestFactory.TriviaQuestionActivatedAt;

    [Fact]
    public void Create_WhileQuestionActive_HasNoAwaitingRevealSequenceOrder()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var dto = Build(session, Now.AddSeconds(5));

        dto.ActiveQuestion.Should().NotBeNull();
        dto.AwaitingRevealQuestionSequenceOrder.Should().BeNull();
    }

    [Fact]
    public void Create_DuringRevealWindow_MidSubstage_ExposesJustClosedSequenceOrder()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        var dto = Build(session, Now.AddSeconds(32));

        // The just-closed question is history (no active window) but its result is being revealed.
        dto.ActiveQuestion.Should().BeNull();
        dto.AwaitingRevealQuestionSequenceOrder.Should().Be(1);
    }

    [Fact]
    public void Create_DuringRevealWindow_LastQuestion_ExposesJustClosedSequenceOrder()
    {
        var session = CreateActiveTrivia(questionCount: 1);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        nextQuestionIndex.Should().BeNull("the substage's last question just closed");
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        var dto = Build(session, Now.AddSeconds(32));

        dto.AwaitingRevealQuestionSequenceOrder.Should().Be(1);
    }

    [Fact]
    public void Create_DuringCloseToAdvanceGap_HasNoAwaitingRevealSequenceOrder()
    {
        // A plain close (no reveal window) leaves nothing awaiting reveal — the field stays null.
        var session = CreateActiveTrivia(questionCount: 2);
        session.CloseActiveQuestion(Now.AddSeconds(30));

        var dto = Build(session, Now.AddSeconds(31));

        dto.AwaitingRevealQuestionSequenceOrder.Should().BeNull();
    }

    [Fact]
    public void Create_WhileQuestionActive_CarriesMissionDeadlineDistinctFromQuestionWindow()
    {
        // The primary Total/RemainingSeconds carry the 30s question window; the mission fields carry the
        // whole-mission deadline (10min) so a client reconnecting mid-question can restore both clocks.
        var session = CreateActiveTrivia(questionCount: 2);
        var dto = Build(session, Now.AddSeconds(5));

        dto.TotalSeconds.Should().Be(30);
        dto.MissionTotalSeconds.Should().Be(600);
        // Session went Active at Now-30s (mission seeded then); observed at Now+5s → 35s elapsed.
        dto.MissionRemainingSeconds.Should().Be(565);
    }

    [Fact]
    public void Create_BeforeSessionStart_HasNoMissionDeadline()
    {
        // The deadline is seeded at session start, so a Scheduled session carries no mission clock.
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: 2);
        var dto = Build(session, Now);

        dto.MissionTotalSeconds.Should().BeNull();
        dto.MissionRemainingSeconds.Should().BeNull();
    }

    [Fact]
    public void Create_WhileQuestionActive_HasNoActiveRankingReveal()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var dto = Build(session, Now.AddSeconds(5));

        dto.ActiveRankingReveal.Should().BeNull();
    }

    [Fact]
    public void Create_DuringNonTerminalSubstageReveal_ExposesTheActiveReveal()
    {
        // First of two trivia substages is revealing: play mode Trivia, not terminal, the window's own
        // deadline, and the revealed substage's id (the pointer has not moved off it yet).
        var session = CreateActiveMultiSubstageTrivia();
        var revealedSubstageId = session.ActiveSubstageId!.Value;
        session.BeginSubstageRankingReveal(Now, LiveSession.SubstageRankingRevealDuration);

        var dto = Build(session, Now.AddSeconds(2));

        dto.ActiveRankingReveal.Should().NotBeNull();
        dto.ActiveRankingReveal!.SubstageSnapshotId.Should().Be(revealedSubstageId);
        dto.ActiveRankingReveal.PlayMode.Should().Be("Trivia");
        dto.ActiveRankingReveal.IsTerminal.Should().BeFalse();
        dto.ActiveRankingReveal.RevealUntil.Should().Be(Now.Add(LiveSession.SubstageRankingRevealDuration));
        dto.ActiveRankingReveal.EmittedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_DuringTreasureHuntReveal_ExposesTerminalRevealForTheLastSubstage()
    {
        var session = CreateActiveTreasureHunt();
        session.BeginSubstageRankingReveal(Now, LiveSession.SubstageRankingRevealDuration);

        var dto = Build(session, Now.AddSeconds(2));

        dto.ActiveRankingReveal.Should().NotBeNull();
        dto.ActiveRankingReveal!.PlayMode.Should().Be("TreasureHunt");
        dto.ActiveRankingReveal.IsTerminal.Should().BeTrue("the hunt is the mission's only, and so last, substage");
    }

    [Fact]
    public void Create_WhilePausedPastRevealDeadline_KeepsTheActiveReveal()
    {
        // Paused sessions are excluded from worker progression, so the reveal is deliberately retained —
        // it must remain in the snapshot even once the original wall-clock RevealUntil has passed.
        var session = CreateActiveMultiSubstageTrivia();
        session.BeginSubstageRankingReveal(Now, LiveSession.SubstageRankingRevealDuration);
        session.MoveTo(SessionState.Paused, Now.AddSeconds(1), new SessionStateTransitionPolicy());

        var dto = Build(session, Now.AddMinutes(5));

        dto.ActiveRankingReveal.Should().NotBeNull();
        dto.ActiveRankingReveal!.RevealUntil.Should().Be(Now.Add(LiveSession.SubstageRankingRevealDuration));
    }

    [Fact]
    public void Create_AfterRevealCompleted_HasNoActiveRankingReveal()
    {
        var session = CreateActiveMultiSubstageTrivia();
        session.BeginSubstageRankingReveal(Now, LiveSession.SubstageRankingRevealDuration);
        session.CompleteSubstageRankingReveal();

        var dto = Build(session, Now.AddSeconds(2));

        dto.ActiveRankingReveal.Should().BeNull();
    }

    [Fact]
    public void Create_ActiveReveal_SerializesToCamelCaseWireShape()
    {
        var session = CreateActiveMultiSubstageTrivia();
        session.BeginSubstageRankingReveal(Now, LiveSession.SubstageRankingRevealDuration);
        var dto = Build(session, Now.AddSeconds(2));

        // JsonSerializerDefaults.Web mirrors the ASP.NET response serializer's camelCasing.
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"activeRankingReveal\":");
        json.Should().Contain("\"substageSnapshotId\":");
        json.Should().Contain("\"playMode\":");
        json.Should().Contain("\"revealUntil\":");
        json.Should().Contain("\"isTerminal\":");
        json.Should().Contain("\"emittedAt\":");
    }

    private static LiveSession CreateActiveMultiSubstageTrivia()
    {
        var session = LiveSessionTestFactory.CreateScheduledMultiSubstageTrivia();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, Now.AddSeconds(-30), policy);
        return session;
    }

    private static LiveSession CreateActiveTreasureHunt()
    {
        var session = LiveSessionTestFactory.CreateScheduledTreasureHunt();
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, Now.AddSeconds(-30), policy);
        return session;
    }

    private static SessionTimerSnapshotDto Build(LiveSession session, DateTimeOffset observedAt) =>
        SessionTimerSnapshotDtoFactory.Create(
            session,
            teamId: null,
            session.GetAuthoritativeSessionTimerSnapshot(observedAt));

    private static LiveSession CreateActiveTrivia(int questionCount)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: questionCount);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, Now.AddSeconds(-30), policy);
        session.ActivateQuestion(0, Now);
        return session;
    }
}
